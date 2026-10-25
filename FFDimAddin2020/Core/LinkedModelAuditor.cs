using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace BimTools.FFTDIM.Core
{
    /// <summary>
    /// Linked-model deep analysis (FFD-10). Goes one level beyond the v1.1
    /// "flag linked references" behaviour:
    ///
    ///   1. Inventories every RevitLinkInstance (name, path, load status, fire-element count).
    ///   2. Resolves each dimension reference that points into a linked model down to the
    ///      actual element inside the link document, and classifies it (fire-fighting or not).
    ///   3. Runs the structural checks across the boundary: broken cross-link references
    ///      (linked element deleted), references into unloaded links, zero-value dimensions
    ///      and zero-length segments that measure linked geometry.
    ///   4. Optional fire-element coverage inside each loaded link: fire-fighting elements
    ///      living in the linked model that no dimension in the active view references.
    ///   5. Duplicate detection with link-aware reference keys (host ids + link:element pairs).
    ///
    /// The audit is read-only — no transaction is opened. Selecting a finding highlights the
    /// host-side element (the dimension itself, or the RevitLinkInstance when the finding is
    /// inside a link, because Revit cannot select nested link elements from a host selection).
    /// </summary>
    public static class LinkedModelAuditor
    {
        // ------------------------------------------------------------------
        //  Public entry point
        // ------------------------------------------------------------------

        public static AuditResult Audit(Document doc, View view, AppSettings settings)
        {
            AppSettings options = settings ?? new AppSettings();
            var result = new AuditResult { DocumentTitle = doc.Title, ViewName = view.Name };
            double zeroToleranceFeet = options.ZeroValueThresholdFeet;

            IList<RevitLinkInstance> links = CollectLinks(doc);
            result.LinkedModelCount = links.Count;
            if (links.Count == 0) return result;

            // Per-link state used by several checks below.
            var linkDocuments = new Dictionary<int, Document>();          // link instance id -> link doc (null when unloaded)
            var linkedFireElements = new Dictionary<int, List<FireElementInfo>>(); // link instance id -> fire elements inside
            var referencedLinkedElements = new HashSet<string>();         // "linkId:linkedElementId" pairs referenced by any dimension

            // ---- 1. Link inventory ---------------------------------------------------
            foreach (RevitLinkInstance instance in links)
            {
                int instanceId = instance.Id.IntegerValue;

                Document linkDoc = GetLinkDocumentSafely(instance);
                linkDocuments[instanceId] = linkDoc;

                if (linkDoc == null)
                {
                    AddIssue(result, DimensionIssueType.LinkedModelUnloaded, IssueSeverity.Warning, instance.Id,
                        "Link " + instance.Id.IntegerValue, "-", view.Name,
                        string.Format("Linked model '{0}' is unloaded or unreadable. Dimensions that reference " +
                                      "this link cannot be resolved and its fire-fighting coverage cannot be checked.",
                            instance.Name));
                    linkedFireElements[instanceId] = new List<FireElementInfo>();
                    continue;
                }

                List<FireElementInfo> fireElements = FireFightingFilter.CollectAll(linkDoc);
                linkedFireElements[instanceId] = fireElements;

                AddIssue(result, DimensionIssueType.LinkedModelInfo, IssueSeverity.Info, instance.Id,
                    "Link " + instance.Id.IntegerValue, "-", view.Name,
                    string.Format("Linked model '{0}' contains {1} fire-fighting element(s). {2}",
                        instance.Name,
                        fireElements.Count,
                        options.LinkedCoverageCheck
                            ? "Coverage of these elements against the dimensions of the active view is checked below."
                            : "Coverage inside linked models is disabled in Settings & Options."));
            }

            // ---- 2-4. Dimension checks across the link boundary ----------------------
            IList<Dimension> dimensions = DimensionScanner.Collect(doc, view.Id, options.IncludeSpotDimensions);
            result.TotalDimensions = dimensions.Count;

            // Link-aware reference keys for duplicate detection.
            var keyGroups = new Dictionary<string, List<Dimension>>();

            foreach (Dimension dim in dimensions)
            {
                string label = "Dimension " + dim.Id.IntegerValue;
                bool referencesLink = false;

                if (dim.References != null)
                {
                    foreach (Reference reference in dim.References)
                    {
                        if (reference == null) continue;
                        if (reference.LinkedElementId == null ||
                            reference.LinkedElementId == ElementId.InvalidElementId) continue;

                        referencesLink = true;
                        var linkInstance = doc.GetElement(reference.ElementId) as RevitLinkInstance;
                        if (linkInstance == null)
                        {
                            AddIssue(result, DimensionIssueType.OrphanReference, IssueSeverity.Warning, dim.Id, label,
                                "-", view.Name,
                                string.Format("Reference claims to come from link {0} but no such link instance " +
                                              "exists in the model.", reference.ElementId.IntegerValue));
                            continue;
                        }

                        int linkId = linkInstance.Id.IntegerValue;
                        referencedLinkedElements.Add(linkId + ":" + reference.LinkedElementId.IntegerValue);

                        Document linkDoc;
                        if (!linkDocuments.TryGetValue(linkId, out linkDoc) || linkDoc == null) continue;

                        Element linkedElement = linkDoc.GetElement(reference.LinkedElementId);
                        if (linkedElement == null)
                        {
                            AddIssue(result, DimensionIssueType.LinkedElementMissing, IssueSeverity.Error, dim.Id,
                                label, "-", view.Name,
                                string.Format("Reference points to element {0} inside '{1}', which no longer " +
                                              "exists in the linked model. The dimension is broken and should be " +
                                              "deleted or re-created.",
                                    reference.LinkedElementId.IntegerValue, linkInstance.Name));
                            continue;
                        }

                        bool isFire = FireFightingFilter.IsFireFighting(linkedElement);

                        string category = linkedElement.Category != null ? linkedElement.Category.Name : "Element";
                        AddIssue(result, DimensionIssueType.LinkedReference, IssueSeverity.Info, dim.Id, label, "-",
                            view.Name,
                            string.Format("Measures {0} '{1}' (id {2}) inside linked model '{3}'.{4}",
                                category, linkedElement.Name, reference.LinkedElementId.IntegerValue,
                                linkInstance.Name,
                                isFire ? " The referenced element is classified as fire-fighting." : ""));
                    }
                }

                // Zero-value checks (host-side values are valid for linked geometry too —
                // the dimension lives in the host document and stores host units).
                if (dim.Value.HasValue && Math.Abs(dim.Value.Value) < zeroToleranceFeet)
                {
                    AddIssue(result, DimensionIssueType.ZeroValue, IssueSeverity.Error, dim.Id, label,
                        UnitHelper.FormatLength(doc, dim.Value.Value), view.Name,
                        string.Format("Dimension measures less than the configured zero-value threshold ({0} mm) — " +
                                      "likely a broken or mis-snapped reference.{1}",
                            options.ZeroValueThresholdMm.ToString("0.0#"),
                            referencesLink ? " At least one reference resolves into a linked model — " +
                                             "check whether the linked element was modified." : ""),
                        true);
                }

                if (dim.Segments != null)
                {
                    foreach (DimensionSegment segment in dim.Segments)
                    {
                        if (segment == null || !segment.Value.HasValue) continue;
                        if (Math.Abs(segment.Value.Value) >= zeroToleranceFeet) continue;
                        AddIssue(result, DimensionIssueType.SegmentZero, IssueSeverity.Error, dim.Id, label,
                            UnitHelper.FormatLength(doc, segment.Value.Value), view.Name,
                            "Chain dimension contains a zero-length segment — inspect the references at that segment.");
                    }
                }

                string key = BuildLinkAwareKey(dim);
                if (key != null)
                {
                    List<Dimension> group;
                    if (!keyGroups.TryGetValue(key, out group))
                    {
                        group = new List<Dimension>();
                        keyGroups[key] = group;
                    }
                    group.Add(dim);
                }
            }

            // ---- Duplicates with link-aware keys ------------------------------------
            foreach (List<Dimension> group in keyGroups.Values)
            {
                if (group.Count < 2) continue;
                group.Sort((a, b) => a.Id.IntegerValue.CompareTo(b.Id.IntegerValue));
                Dimension keeper = group[0];
                for (int i = 1; i < group.Count; i++)
                {
                    Dimension duplicate = group[i];
                    AddIssue(result, DimensionIssueType.Duplicate, IssueSeverity.Warning, duplicate.Id,
                        "Dimension " + duplicate.Id.IntegerValue,
                        duplicate.Value.HasValue ? UnitHelper.FormatLength(doc, duplicate.Value.Value) : "-",
                        view.Name,
                        string.Format("Same reference geometry (including linked elements) as Dimension {0} — redundant copy.",
                            keeper.Id.IntegerValue),
                        true);
                }
            }

            // ---- 5. Fire-element coverage inside each loaded link --------------------
            if (options.LinkedCoverageCheck)
            {
                foreach (RevitLinkInstance instance in links)
                {
                    int linkId = instance.Id.IntegerValue;
                    List<FireElementInfo> fireElements;
                    if (!linkedFireElements.TryGetValue(linkId, out fireElements)) continue;

                    foreach (FireElementInfo element in fireElements)
                    {
                        if (referencedLinkedElements.Contains(linkId + ":" + element.Id.IntegerValue))
                        {
                            result.CoveredFireElements++;
                            continue;
                        }

                        result.TotalFireElements++;
                        AddIssue(result, DimensionIssueType.LinkedCoverageGap, IssueSeverity.Warning, instance.Id,
                            string.Format("{0} (in '{1}')", element, instance.Name), "-", view.Name,
                            string.Format("Fire-fighting element id {0} inside linked model '{1}' is not referenced " +
                                          "by any dimension of this view. Selecting the finding highlights the link " +
                                          "instance (nested link elements cannot be selected directly from the host).",
                                element.Id.IntegerValue, instance.Name));
                    }
                }
            }

            DimensionAuditor.SortIssues(result);
            return result;
        }

        // ------------------------------------------------------------------
        //  Helpers
        // ------------------------------------------------------------------

        /// <summary>Collects the RevitLinkInstance elements of the host document.</summary>
        public static IList<RevitLinkInstance> CollectLinks(Document doc)
        {
            return new FilteredElementCollector(doc)
                .OfClass(typeof(RevitLinkInstance))
                .Cast<RevitLinkInstance>()
                .Where(l => l != null)
                .OrderBy(l => l.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        /// <summary>Returns the link document, or null when the link is unloaded (or the API refuses).</summary>
        private static Document GetLinkDocumentSafely(RevitLinkInstance instance)
        {
            try
            {
                return instance.GetLinkDocument();
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Duplicate-detection key that also encodes linked-element identity. Two dimensions
        /// that measure the same host + linked geometry share a key; a host-only dimension and
        /// a linked variant of the same geometry do not collide because the linked part is
        /// written as "L:linkId:linkedId" segments.
        /// </summary>
        private static string BuildLinkAwareKey(Dimension dim)
        {
            var parts = new List<string>();
            bool any = false;

            if (dim.References != null)
            {
                foreach (Reference reference in dim.References)
                {
                    if (reference == null) continue;
                    if (reference.LinkedElementId != null && reference.LinkedElementId != ElementId.InvalidElementId)
                    {
                        int linkId = reference.ElementId != null ? reference.ElementId.IntegerValue : 0;
                        parts.Add("L:" + linkId + ":" + reference.LinkedElementId.IntegerValue);
                    }
                    else if (reference.ElementId != null && reference.ElementId != ElementId.InvalidElementId)
                    {
                        parts.Add("H:" + reference.ElementId.IntegerValue);
                    }
                    any = true;
                }
            }

            if (!any) return null;
            parts.Sort(StringComparer.Ordinal);
            return string.Join("|", parts);
        }

        private static void AddIssue(AuditResult result, DimensionIssueType type, IssueSeverity severity,
            ElementId elementId, string label, string valueText, string viewName, string detail, bool canDelete = false)
        {
            result.Issues.Add(new DimensionIssue
            {
                Type = type,
                Severity = severity,
                ElementId = elementId,
                ElementLabel = label,
                ValueText = valueText,
                ViewName = viewName,
                Detail = detail,
                CanDelete = canDelete
            });
        }
    }
}
