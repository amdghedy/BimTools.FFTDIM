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
    /// One topic read from a BCF package, plus the import result against the current model.
    /// Component references carry the raw AuthoringToolId strings; <see cref="ResolveIds"/>
    /// turns them into host-model ElementIds for selection and re-verification.
    /// </summary>
    public class BcfTopicRecord
    {
        public string Guid { get; set; }
        public string Title { get; set; }
        public string Status { get; set; }
        public string TopicType { get; set; }
        public string Author { get; set; }
        public string CreationDate { get; set; }
        public string Description { get; set; }
        public List<string> Comments { get; } = new List<string>();

        /// <summary>Raw AuthoringToolId values found in the topic's viewpoints.</summary>
        public List<string> ComponentRefs { get; } = new List<string>();

        /// <summary>Host-model elements resolved from ComponentRefs (populated by ResolveIds).</summary>
        public List<ElementId> ResolvedIds { get; } = new List<ElementId>();

        public int ResolvedCount
        {
            get { return ResolvedIds.Count; }
        }

        /// <summary>Grid-friendly "resolved / total" text for the preview window.</summary>
        public string ResolvedText
        {
            get { return ResolvedCount + " / " + ComponentRefs.Count; }
        }

        /// <summary>Turns AuthoringToolId values into ElementIds of the current document.
        /// FFTDIM writes decimal Revit element ids; other tools may write anything, so
        /// every non-numeric or unknown id is counted as unresolved rather than failing.</summary>
        public void ResolveIds(Document doc)
        {
            ResolvedIds.Clear();
            foreach (string reference in ComponentRefs)
            {
                int id;
                if (!int.TryParse(reference, out id)) continue;
                if (id <= 0) continue;

                Element element = doc.GetElement(new ElementId(id));
                if (element == null) continue;

                var elementId = new ElementId(id);
                if (!ResolvedIds.Contains(elementId)) ResolvedIds.Add(elementId);
            }
        }
    }

    /// <summary>
    /// Reads a BCF 2.1 package (the export format of <see cref="BcfExporter"/>) back into
    /// memory. Parsing is namespace-tolerant (matching by local element name) so packages
    /// produced by other BCF tools with slightly different namespaces still import.
    /// </summary>
    public static class BcfImporter
    {
        /// <summary>Reads the package and returns its topics in file order.</summary>
        public static List<BcfTopicRecord> Read(string path)
        {
            using (FileStream stream = File.OpenRead(path))
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Read))
            {
                ValidatePackage(archive);
                var topics = new List<BcfTopicRecord>();

                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    if (!entry.FullName.EndsWith("markup.bcf", StringComparison.OrdinalIgnoreCase)) continue;

                    string folder = GetFolderName(entry.FullName);
                    XDocument markup = LoadXml(entry);
                    if (markup == null || markup.Root == null) continue;

                    BcfTopicRecord topic = ParseMarkup(markup.Root);
                    topic.Guid = folder;

                    // Component references live in the viewpoints of the same folder.
                    foreach (string viewpointName in ParseViewpointNames(markup.Root))
                    {
                        string viewpointPath = (folder.Length > 0 ? folder + "/" : "") + viewpointName;
                        ZipArchiveEntry viewpointEntry = FindEntry(archive, viewpointPath);
                        if (viewpointEntry == null) continue;

                        XDocument viewpoint = LoadXml(viewpointEntry);
                        if (viewpoint == null || viewpoint.Root == null) continue;
                        CollectComponentRefs(viewpoint.Root, topic.ComponentRefs);
                    }

                    if (topic.ComponentRefs.Count == 0)
                    {
                        // No explicit viewpoint list — scan every .bcfv in the folder anyway.
                        foreach (ZipArchiveEntry viewpointEntry in archive.Entries)
                        {
                            if (!viewpointEntry.FullName.StartsWith(folder + "/", StringComparison.OrdinalIgnoreCase) ||
                                !viewpointEntry.FullName.EndsWith(".bcfv", StringComparison.OrdinalIgnoreCase)) continue;

                            XDocument viewpoint = LoadXml(viewpointEntry);
                            if (viewpoint == null || viewpoint.Root == null) continue;
                            CollectComponentRefs(viewpoint.Root, topic.ComponentRefs);
                        }
                    }

                    topics.Add(topic);
                }

                return topics;
            }
        }

        // ------------------------------------------------------------------
        //  Package / XML plumbing
        // ------------------------------------------------------------------

        private static void ValidatePackage(ZipArchive archive)
        {
            bool hasVersion = archive.Entries.Any(
                e => e.FullName.Equals("bcf.version", StringComparison.OrdinalIgnoreCase));
            if (!hasVersion)
            {
                throw new InvalidDataException(
                    "The file does not look like a BCF package (no bcf.version entry). " +
                    "Export one from FFTDIM ('BCF Export') or your BCF platform first.");
            }

            bool hasMarkup = archive.Entries.Any(
                e => e.FullName.EndsWith("markup.bcf", StringComparison.OrdinalIgnoreCase));
            if (!hasMarkup)
            {
                throw new InvalidDataException("The BCF package contains no topics (no markup.bcf entries).");
            }
        }

        private static ZipArchiveEntry FindEntry(ZipArchive archive, string fullName)
        {
            return archive.Entries.FirstOrDefault(
                e => string.Equals(e.FullName, fullName, StringComparison.OrdinalIgnoreCase));
        }

        private static XDocument LoadXml(ZipArchiveEntry entry)
        {
            try
            {
                using (Stream stream = entry.Open())
                using (var reader = new StreamReader(stream, Encoding.UTF8))
                {
                    return XDocument.Parse(reader.ReadToEnd());
                }
            }
            catch
            {
                return null;
            }
        }

        private static string GetFolderName(string entryFullName)
        {
            int slash = entryFullName.IndexOf('/');
            return slash > 0 ? entryFullName.Substring(0, slash) : string.Empty;
        }

        private static IEnumerable<string> ParseViewpointNames(XElement markupRoot)
        {
            var names = new List<string>();
            foreach (XElement viewpoints in markupRoot.ElementsLocal("Viewpoints"))
            {
                foreach (XElement viewpoint in viewpoints.ElementsLocal("Viewpoint"))
                {
                    string file = (string)viewpoint.Attribute("Viewpoint");
                    if (!string.IsNullOrEmpty(file)) names.Add(file);
                }
            }
            return names;
        }

        private static BcfTopicRecord ParseMarkup(XElement markupRoot)
        {
            var topic = new BcfTopicRecord();

            XElement topicElement = markupRoot.ElementsLocal("Topic").FirstOrDefault();
            if (topicElement != null)
            {
                topic.Guid = (string)topicElement.Attribute("Guid") ?? topic.Guid;
                topic.Title = (string)topicElement.Attribute("Title") ?? "(untitled)";
                topic.Status = (string)topicElement.Attribute("TopicStatus") ?? "Open";
                topic.TopicType = (string)topicElement.Attribute("TopicType") ?? "Issue";
                topic.Author = (string)topicElement.Attribute("CreationAuthor") ?? "unknown";
                topic.CreationDate = (string)topicElement.Attribute("CreationDate") ?? "";

                XElement description = topicElement.ElementsLocal("Description").FirstOrDefault();
                topic.Description = description != null ? description.Value : "";
            }

            foreach (XElement comments in markupRoot.ElementsLocal("Comments"))
            {
                foreach (XElement comment in comments.ElementsLocal("Comment"))
                {
                    XElement body = comment.ElementsLocal("Comment").FirstOrDefault();
                    string author = (string)comment.Attribute("Author");
                    string text = body != null ? body.Value.Trim() : "";
                    if (text.Length > 0)
                    {
                        topic.Comments.Add(string.IsNullOrEmpty(author) ? text : author + ": " + text);
                    }
                }
            }

            if (string.IsNullOrEmpty(topic.Title)) topic.Title = "(untitled)";
            return topic;
        }

        /// <summary>Collects AuthoringToolId references from Selection and Visibility exceptions.</summary>
        private static void CollectComponentRefs(XElement viewpointRoot, List<string> into)
        {
            foreach (XElement components in viewpointRoot.ElementsLocal("Components"))
            {
                foreach (XElement selection in components.ElementsLocal("Selection"))
                {
                    CollectComponents(selection, into);
                }

                foreach (XElement visibility in components.ElementsLocal("Visibility"))
                {
                    foreach (XElement exceptions in visibility.ElementsLocal("Exceptions"))
                    {
                        CollectComponents(exceptions, into);
                    }
                }
            }
        }

        private static void CollectComponents(XElement container, List<string> into)
        {
            foreach (XElement component in container.ElementsLocal("Component"))
            {
                string authoringToolId = (string)component.Attribute("AuthoringToolId");
                if (string.IsNullOrEmpty(authoringToolId)) continue;
                if (into.Contains(authoringToolId)) continue;
                into.Add(authoringToolId);
            }
        }
    }

    /// <summary>Local-name LINQ-to-XML helpers so BCF parsing stays namespace-tolerant.</summary>
    internal static class BcfXmlExtensions
    {
        public static IEnumerable<XElement> ElementsLocal(this XContainer container, string localName)
        {
            return container.Elements().Where(e => e.Name.LocalName == localName);
        }
    }
}
