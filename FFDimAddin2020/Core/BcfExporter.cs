using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using Autodesk.Revit.DB;

namespace BimTools.FFTDIM.Core
{
    /// <summary>
    /// Writes an <see cref="AuditResult"/> as a BCF 2.1 package (BIM Collaboration Format,
    /// buildingSMART) for issue round-trips with BCF-capable platforms (BIMcollab, Revizto,
    /// Autodesk Construction Cloud, Solibri, Trimble Connect, ...).
    ///
    /// Package layout written by this class:
    /// <code>
    ///   FFTDIM_Audit_yyyyMMdd_HHmm.bcf   (ZIP)
    ///   ├── bcf.version                  VersionId 2.1
    ///   ├── project.bcfp                 project name = host document title
    ///   └── {topicGuid}/
    ///       ├── markup.bcf               topic, description, comments
    ///       └── viewpoint.bcfv           camera + component selection (optional)
    /// </code>
    ///
    /// Interoperability notes (also documented in the README):
    ///   · Components are referenced through the AuthoringToolId attribute holding the Revit
    ///     element id as a decimal string — the usual convention for native (non-IFC) sources.
    ///   · Camera coordinates are written in metres (BCF expects SI units; the Revit internal
    ///     unit is the foot — converted with a fixed 0.3048 factor).
    ///   · Snapshots are omitted: capturing one would require UI-level screen capture that
    ///     does not run reliably inside a modal command. Viewers fall back to the camera.
    /// </summary>
    public static class BcfExporter
    {
        private const string MarkupNs = "http://www.buildingsmart-tech.org/xml/bcf/2.1/markup";
        private const string ProjectNs = "http://www.buildingsmart-tech.org/xml/bcf/2.1/project";
        private const string VersionNs = "http://www.buildingsmart-tech.org/xml/bcf/2.1/version";
        private const string ViewpointNs = "http://www.buildingsmart-tech.org/xml/bcf/2.1/viewpoint";
        private const double FeetToMeters = 0.3048;
        private const int MaxComponentsPerTopic = 50;

        /// <summary>
        /// Writes the BCF package. <paramref name="view"/> supplies the camera for the
        /// viewpoints; pass null (or disable cameras in the settings) to write topics
        /// without viewpoints. Returns the written path.
        /// </summary>
        public static string Write(string path, AuditResult result, View view, AppSettings settings)
        {
            AppSettings options = settings ?? new AppSettings();
            List<BcfTopic> topics = BuildTopics(result, options, view);

            using (FileStream stream = File.Create(path))
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Create))
            {
                WriteEntry(archive, "bcf.version", BuildVersionXml());
                WriteEntry(archive, "project.bcfp", BuildProjectXml(result != null ? result.DocumentTitle : null));

                foreach (BcfTopic topic in topics)
                {
                    string folder = topic.Guid + "/";
                    WriteEntry(archive, folder + "markup.bcf", BuildMarkupXml(topic));
                    if (topic.HasViewpoint)
                    {
                        WriteEntry(archive, folder + "viewpoint.bcfv", BuildViewpointXml(topic));
                    }
                }
            }

            return path;
        }

        // ------------------------------------------------------------------
        //  Topic assembly
        // ------------------------------------------------------------------

        /// <summary>Groups the audit findings into BCF topics according to the topic mode.</summary>
        private static List<BcfTopic> BuildTopics(AuditResult result, AppSettings options, View view)
        {
            var topics = new List<BcfTopic>();
            if (result == null || result.Issues.Count == 0) return topics;

            string creationDate = DateTime.UtcNow.ToString("o");
            string sourceFile = SanitizeFileName(result.DocumentTitle);
            CameraSnapshot camera = null;
            if (options.BcfIncludeCamera && view != null)
            {
                camera = TryGetCamera(view);
            }

            if (options.BcfTopicPerIssue)
            {
                int index = 1;
                foreach (DimensionIssue issue in result.Issues)
                {
                    var topic = MakeTopic(
                        string.Format("[{0}] {1}", issue.Type, issue.ElementLabel ?? "Finding"),
                        JoinNonEmpty("\n", new[]
                        {
                            "Severity: " + issue.Severity,
                            "Element: " + ElementText(issue),
                            "Value: " + (issue.ValueText ?? "-"),
                            "View: " + (issue.ViewName ?? "-"),
                            "",
                            issue.Detail ?? ""
                        }),
                        new List<DimensionIssue> { issue },
                        index++);
                    FinalizeTopic(topic, creationDate, sourceFile, camera);
                    topics.Add(topic);
                }
            }
            else
            {
                List<DimensionIssue> issues = result.Issues;
                var description = new StringBuilder();
                description.AppendFormat("FFTDIM audit of view '{0}' on document '{1}' at {2:yyyy-MM-dd HH:mm}.\n",
                    result.ViewName ?? "-", result.DocumentTitle ?? "-", result.Timestamp);
                if (camera == null) description.Append("No camera available for this view.\n");
                description.AppendLine();

                int shown = 0;
                foreach (DimensionIssue issue in issues)
                {
                    if (shown >= 25)
                    {
                        description.AppendFormat("… and {0} more findings.\n", issues.Count - shown);
                        break;
                    }
                    description.AppendFormat("· [{0}/{1}] {2} — {3} ({4})\n",
                        issue.Severity, issue.Type, issue.ElementLabel ?? "-", issue.ValueText ?? "-", issue.Detail ?? "");
                    shown++;
                }

                var topic = MakeTopic(
                    string.Format("FFTDIM audit — {0} ({1} finding{2})",
                        result.ViewName ?? "-", issues.Count, issues.Count == 1 ? "" : "s"),
                    description.ToString().TrimEnd(),
                    issues,
                    1);
                FinalizeTopic(topic, creationDate, sourceFile, camera);
                topics.Add(topic);
            }

            return topics;
        }

        private static BcfTopic MakeTopic(string title, string description, List<DimensionIssue> issues, int index)
        {
            var componentIds = new List<int>();
            foreach (DimensionIssue issue in issues)
            {
                if (issue.ElementId == null || issue.ElementId == ElementId.InvalidElementId) continue;
                if (componentIds.Contains(issue.ElementId.IntegerValue)) continue;
                if (componentIds.Count >= MaxComponentsPerTopic) break;
                componentIds.Add(issue.ElementId.IntegerValue);
            }

            return new BcfTopic
            {
                Guid = Guid.NewGuid().ToString("D"),
                Title = title,
                Description = description,
                Index = index,
                Issues = issues,
                ComponentIds = componentIds
            };
        }

        /// <summary>Assigns creation date, source file and (optionally) a viewpoint to a topic.</summary>
        private static void FinalizeTopic(BcfTopic topic, string creationDate, string sourceFile, CameraSnapshot camera)
        {
            topic.CreationDateIso = creationDate;
            topic.SourceFile = sourceFile;
            if (camera != null)
            {
                topic.HasViewpoint = true;
                topic.ViewpointGuid = Guid.NewGuid().ToString("D");
                topic.Camera = camera;
            }
        }

        // ------------------------------------------------------------------
        //  XML builders
        // ------------------------------------------------------------------

        private static string BuildVersionXml()
        {
            return new XDocument(
                new XDeclaration("1.0", "UTF-8", "true"),
                new XElement(VersionNs + "Version",
                    new XAttribute("VersionId", "2.1"),
                    new XAttribute("DetailedVersion", "2.1"))).ToString();
        }

        private static string BuildProjectXml(string documentTitle)
        {
            return new XDocument(
                new XDeclaration("1.0", "UTF-8", "true"),
                new XElement(ProjectNs + "ProjectExtension",
                    new XElement(ProjectNs + "Project",
                        new XAttribute("ProjectId", SanitizeProjectId(documentTitle)),
                        new XElement(ProjectNs + "Name", documentTitle ?? "unknown")))).ToString();
        }

        private static string BuildMarkupXml(BcfTopic topic)
        {
            var markup = new XElement(MarkupNs + "Markup",
                new XElement(MarkupNs + "Header",
                    new XElement(MarkupNs + "File",
                        new XAttribute("Filename", topic.SourceFile))),
                new XElement(MarkupNs + "Topic",
                    new XAttribute("Guid", topic.Guid),
                    new XAttribute("TopicType", "Issue"),
                    new XAttribute("TopicStatus", "Open"),
                    new XAttribute("Title", Clip(topic.Title, 180)),
                    new XAttribute("Priority", "Normal"),
                    new XAttribute("Index", topic.Index),
                    new XAttribute("CreationDate", topic.CreationDateIso),
                    new XAttribute("CreationAuthor", "BimTools.FFTDIM"),
                    new XElement(MarkupNs + "Description", new XCData(topic.Description ?? "")),
                    new XElement(MarkupNs + "Labels",
                        new XElement(MarkupNs + "Label", "FFTDIM"),
                        new XElement(MarkupNs + "Label", string.Format("{0} finding{1}",
                            topic.Issues.Count, topic.Issues.Count == 1 ? "" : "s")))));

            if (topic.HasViewpoint)
            {
                markup.Add(new XElement(MarkupNs + "Viewpoints",
                    new XElement(MarkupNs + "Viewpoint",
                        new XAttribute("Guid", topic.ViewpointGuid),
                        new XAttribute("Viewpoint", "viewpoint.bcfv"))));
            }

            var comments = new XElement(MarkupNs + "Comments");
            foreach (DimensionIssue issue in topic.Issues)
            {
                comments.Add(new XElement(MarkupNs + "Comment",
                    new XAttribute("Guid", Guid.NewGuid().ToString("D")),
                    new XAttribute("Date", topic.CreationDateIso),
                    new XAttribute("Author", "BimTools.FFTDIM"),
                    new XElement(MarkupNs + "Comment",
                        new XCData(string.Format("[{0}/{1}] {2} — {3}. {4}",
                            issue.Severity, issue.Type,
                            ElementText(issue),
                            issue.ValueText ?? "-",
                            issue.Detail ?? "")))));
            }
            markup.Add(comments);

            return new XDocument(new XDeclaration("1.0", "UTF-8", "true"), markup).ToString();
        }

        /// <summary>Builds viewpoint.bcfv with the stored camera and component selection.</summary>
        private static string BuildViewpointXml(BcfTopic topic)
        {
            CameraSnapshot camera = topic.Camera;

            var visualization = new XElement(ViewpointNs + "VisualizationInfo",
                new XElement(ViewpointNs + "PerspectiveCamera",
                    Vector("CameraViewPoint", camera.Eye),
                    Vector("CameraDirection", camera.Forward),
                    Vector("CameraUpVector", camera.Up),
                    new XElement(ViewpointNs + "FieldOfView", "60")),
                new XElement(ViewpointNs + "Components",
                    new XElement(ViewpointNs + "ViewSetupHints",
                        new XAttribute("SpaceVisible", "true"),
                        new XAttribute("SpaceBoundVisible", "true"))));

            if (topic.ComponentIds != null && topic.ComponentIds.Count > 0)
            {
                XElement components = visualization.Element(ViewpointNs + "Components");

                var selection = new XElement(ViewpointNs + "Selection");
                foreach (int id in topic.ComponentIds)
                {
                    selection.Add(new XElement(ViewpointNs + "Component",
                        new XAttribute("AuthoringToolId", id.ToString())));
                }
                components.Add(selection);

                components.Add(new XElement(ViewpointNs + "Visibility",
                    new XElement(ViewpointNs + "ViewSetupHints",
                        new XAttribute("SpaceVisible", "true"),
                        new XAttribute("SpaceBoundVisible", "true")),
                    new XElement(ViewpointNs + "DefaultVisibility", "true")));
            }

            return new XDocument(new XDeclaration("1.0", "UTF-8", "true"), visualization).ToString();
        }

        /// <summary>Writes a BCF direction/position triple: element wrapping X/Y/Z children in metres.</summary>
        private static XElement Vector(string containerName, XYZ point)
        {
            double x = point.X * FeetToMeters;
            double y = point.Y * FeetToMeters;
            double z = point.Z * FeetToMeters;

            // Direction vectors are unitless; converting through the scale factor keeps
            // them unit vectors only if they already are (Revit returns them normalised).
            if (containerName != "CameraViewPoint")
            {
                x = point.X; y = point.Y; z = point.Z;
            }

            return new XElement(ViewpointNs + containerName,
                new XElement(ViewpointNs + "X", Round(x)),
                new XElement(ViewpointNs + "Y", Round(y)),
                new XElement(ViewpointNs + "Z", Round(z)));
        }

        // ------------------------------------------------------------------
        //  Camera resolution
        // ------------------------------------------------------------------

        private class CameraSnapshot
        {
            public XYZ Eye { get; set; }
            public XYZ Forward { get; set; }
            public XYZ Up { get; set; }
        }

        /// <summary>
        /// Derives a BCF perspective camera for the view. 3D views use their stored
        /// orientation; 2D views get a synthesized camera above the crop box centre
        /// looking along the view normal. Returns null when nothing usable exists.
        /// </summary>
        private static CameraSnapshot TryGetCamera(View view)
        {
            try
            {
                var view3d = view as View3D;
                if (view3d != null)
                {
                    ViewOrientation3D orientation = view3d.GetOrientation();
                    return new CameraSnapshot
                    {
                        Eye = orientation.EyePosition,
                        Forward = orientation.ForwardDirection.Normalize(),
                        Up = orientation.UpDirection.Normalize()
                    };
                }

                BoundingBoxXYZ box = view.CropBox;
                if (box == null || box.Transform == null || box.Min == null || box.Max == null) return null;

                XYZ min = box.Min;
                XYZ max = box.Max;
                XYZ centerLocal = new XYZ(
                    (min.X + max.X) / 2.0,
                    (min.Y + max.Y) / 2.0,
                    (min.Z + max.Z) / 2.0);
                double diagonal = Math.Max(
                    Math.Max(Math.Abs(max.X - min.X), Math.Abs(max.Y - min.Y)),
                    Math.Abs(max.Z - min.Z));

                Transform transform = box.Transform;
                XYZ center = transform.OfPoint(centerLocal);
                double distance = Math.Max(diagonal, 5.0 / FeetToMeters) * 1.5;

                // BasisZ of a 2D view points out of the screen toward the viewer.
                XYZ normal = transform.BasisZ.Normalize();
                return new CameraSnapshot
                {
                    Eye = center + normal * distance,
                    Forward = -normal,
                    Up = transform.BasisY.Normalize()
                };
            }
            catch
            {
                return null;
            }
        }

        // ------------------------------------------------------------------
        //  Small helpers
        // ------------------------------------------------------------------

        private static void WriteEntry(ZipArchive archive, string name, string content)
        {
            ZipArchiveEntry entry = archive.CreateEntry(name, CompressionLevel.Optimal);
            using (Stream entryStream = entry.Open())
            {
                byte[] bytes = new UTF8Encoding(false).GetBytes(content);
                entryStream.Write(bytes, 0, bytes.Length);
            }
        }

        private static string SanitizeFileName(string documentTitle)
        {
            if (string.IsNullOrEmpty(documentTitle)) return "project.rvt";
            string name = documentTitle;
            foreach (char invalid in System.IO.Path.GetInvalidFileNameChars())
            {
                name = name.Replace(invalid, '_');
            }
            return name.EndsWith(".rvt", StringComparison.OrdinalIgnoreCase) ? name : name + ".rvt";
        }

        private static string SanitizeProjectId(string documentTitle)
        {
            if (string.IsNullOrEmpty(documentTitle)) return "project";
            var builder = new StringBuilder();
            foreach (char c in documentTitle)
            {
                builder.Append(char.IsLetterOrDigit(c) || c == '-' || c == '_' ? c : '_');
            }
            string id = builder.ToString();
            return id.Length > 60 ? id.Substring(0, 60) : id;
        }

        private static string Clip(string text, int max)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= max) return text ?? "";
            return text.Substring(0, max - 3) + "...";
        }

        private static string JoinNonEmpty(string separator, string[] parts)
        {
            return string.Join(separator, parts.Where(p => !string.IsNullOrEmpty(p)));
        }

        private static string Round(double value)
        {
            return value.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture);
        }

        private static string ElementText(DimensionIssue issue)
        {
            string label = issue.ElementLabel ?? "-";
            if (issue.ElementId != null && issue.ElementId != ElementId.InvalidElementId)
            {
                label += " (id " + issue.ElementId.IntegerValue + ")";
            }
            return label;
        }

        /// <summary>One BCF topic plus everything needed to serialise it.</summary>
        private class BcfTopic
        {
            public string Guid { get; set; }
            public string Title { get; set; }
            public string Description { get; set; }
            public int Index { get; set; }
            public List<DimensionIssue> Issues { get; set; }
            public List<int> ComponentIds { get; set; }
            public bool HasViewpoint { get; set; }
            public string ViewpointGuid { get; set; }
            public CameraSnapshot Camera { get; set; }
            public string CreationDateIso { get; set; }

            public string SourceFile { get; set; }
        }
    }
}
