using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace BimTools.FFTDIM.Core
{
    /// <summary>
    /// The audit engine. Produces an <see cref="AuditResult"/> for one view using three
    /// independent checks:
    ///   Audit          — zero-value dims, orphan references, zero-length chain segments, duplicates.
    ///   CoverageAudit  — fire-fighting elements not referenced by any dimension in the view.
    ///   ToleranceAudit — dimension values checked against user-configured tolerance rules.
    /// </summary>
    public static class DimensionAuditor
    {
        // ------------------------------------------------------------------
        //  1. Standard dimension audit
        // ------------------------------------------------------------------

        public static AuditResult Audit(Document doc, View view)
        {
            var result = new AuditResult { DocumentTitle = doc.Title, ViewName = view.Name };
            IList<Dimension> dimensions = DimensionScanner.Collect(doc, view.Id);
            result.TotalDimensions = dimensions.Count;

            foreach (Dimension dim in dimensions)
            {
                string label = "Dimension " + dim.Id.IntegerValue;

                // Orphan references (element behind a reference no longer exists).
                foreach (ElementId referenceId in DimensionScanner.GetReferenceElementIds(dim))
                {
                    if (doc.GetElement(referenceId) != null) continue;
                    AddIssue(result, DimensionIssueType.OrphanReference, IssueSeverity.Warning, dim.Id, label, "-",
                        view.Name, string.Format("Reference points to element {0} which no longer exists in the model.", referenceId.IntegerValue));
                    break;
                }

                // Zero-value dimension.
                if (dim.Value.HasValue && Math.Abs(dim.Value.Value) < DimensionScanner.ZeroValueToleranceFeet)
                {
                    AddIssue(result, DimensionIssueType.ZeroValue, IssueSeverity.Error, dim.Id, label,
                        UnitHelper.FormatLength(doc, dim.Value.Value), view.Name,
                        "Dimension measures (almost) zero — likely a broken or mis-snapped reference. Safe to delete.",
                        true);
                }

                // Zero-length segment inside a chain dimension.
                if (dim.Segments != null)
                {
                    foreach (DimensionSegment segment in dim.Segments)
                    {
                        if (segment == null || !segment.Value.HasValue) continue;
                        if (Math.Abs(segment.Value.Value) >= DimensionScanner.ZeroValueToleranceFeet) continue;
                        AddIssue(result, DimensionIssueType.SegmentZero, IssueSeverity.Error, dim.Id, label,
                            UnitHelper.FormatLength(doc, segment.Value.Value), view.Name,
                            "Chain dimension contains a zero-length segment — inspect the references at that segment.");
                    }
                }
            }

            // Duplicates (same reference set as another dimension).
            foreach (List<Dimension> group in RedundancyFinder.FindDuplicateGroups(dimensions))
            {
                group.Sort((a, b) => a.Id.IntegerValue.CompareTo(b.Id.IntegerValue));
                Dimension keeper = group[0];
                for (int i = 1; i < group.Count; i++)
                {
                    Dimension duplicate = group[i];
                    string valueText = duplicate.Value.HasValue
                        ? UnitHelper.FormatLength(doc, duplicate.Value.Value)
                        : "-";
                    AddIssue(result, DimensionIssueType.Duplicate, IssueSeverity.Warning, duplicate.Id,
                        "Dimension " + duplicate.Id.IntegerValue, valueText, view.Name,
                        string.Format("Same references and value as Dimension {0} — redundant copy.", keeper.Id.IntegerValue),
                        true);
                }
            }

            SortIssues(result);
            return result;
        }

        // ------------------------------------------------------------------
        //  2. Fire-fighting coverage audit
        // ------------------------------------------------------------------

        public static AuditResult CoverageAudit(Document doc, View view)
        {
            var result = new AuditResult { DocumentTitle = doc.Title, ViewName = view.Name };

            IList<Dimension> dimensions = DimensionScanner.Collect(doc, view.Id);
            result.TotalDimensions = dimensions.Count;

            // Set of every element referenced by any dimension in the view.
            var referencedIds = new HashSet<int>();
            foreach (Dimension dim in dimensions)
            {
                foreach (ElementId id in DimensionScanner.GetReferenceElementIds(dim))
                {
                    referencedIds.Add(id.IntegerValue);
                }
            }

            List<FireElementInfo> fireElements = FireFightingFilter.CollectInView(doc, view);
            result.TotalFireElements = fireElements.Count;

            foreach (FireElementInfo element in fireElements)
            {
                element.IsDimensioned = referencedIds.Contains(element.Id.IntegerValue);
                if (element.IsDimensioned)
                {
                    result.CoveredFireElements++;
                    continue;
                }

                AddIssue(result, DimensionIssueType.CoverageGap, IssueSeverity.Warning, element.Id,
                    element.ToString(), "-", view.Name,
                    string.Format("{0} is not referenced by any dimension in this view.", element.ToString()));
            }

            SortIssues(result);
            return result;
        }

        // ------------------------------------------------------------------
        //  3. Tolerance audit
        // ------------------------------------------------------------------

        public static AuditResult ToleranceAudit(Document doc, View view, List<ToleranceRule> rules)
        {
            var result = new AuditResult { DocumentTitle = doc.Title, ViewName = view.Name };

            var enabled = new List<ToleranceRule>();
            foreach (ToleranceRule rule in rules)
            {
                if (rule.Enabled && (rule.MinMeters > 0 || rule.MaxMeters > 0)) enabled.Add(rule);
            }
            if (enabled.Count == 0) return result;

            IList<Dimension> dimensions = DimensionScanner.Collect(doc, view.Id);
            result.TotalDimensions = dimensions.Count;

            foreach (Dimension dim in dimensions)
            {
                List<Element> referencedElements = DimensionScanner.GetReferenceElementIds(dim)
                    .Select(id => doc.GetElement(id))
                    .Where(e => e != null)
                    .ToList();
                if (referencedElements.Count == 0) continue;

                string scope = ClassifyScope(referencedElements);
                string label = "Dimension " + dim.Id.IntegerValue;

                foreach (double value in DimensionScanner.GetValues(doc, dim))
                {
                    double valueMeters = UnitHelper.MetersFromFeet(value);
                    string valueText = UnitHelper.FormatLength(doc, value);

                    foreach (ToleranceRule rule in enabled)
                    {
                        if (!MatchesScope(rule.Scope, scope)) continue;

                        if (rule.MinMeters > 0 && valueMeters < rule.MinMeters - 1e-9)
                        {
                            AddIssue(result, DimensionIssueType.ToleranceViolation, IssueSeverity.Error, dim.Id,
                                label, valueText, view.Name,
                                string.Format("Value is below the minimum {0} m set by rule '{1}' (scope: {2}).",
                                    FormatMeters(rule.MinMeters), rule.Name, scope));
                        }
                        else if (rule.MaxMeters > 0 && valueMeters > rule.MaxMeters + 1e-9)
                        {
                            AddIssue(result, DimensionIssueType.ToleranceViolation, IssueSeverity.Warning, dim.Id,
                                label, valueText, view.Name,
                                string.Format("Value is above the maximum {0} m set by rule '{1}' (scope: {2}).",
                                    FormatMeters(rule.MaxMeters), rule.Name, scope));
                        }
                    }
                }
            }

            SortIssues(result);
            return result;
        }

        // ------------------------------------------------------------------
        //  Helpers
        // ------------------------------------------------------------------

        private static string ClassifyScope(List<Element> elements)
        {
            foreach (Element element in elements)
            {
                if (element.Category == null) continue;
                int categoryId = element.Category.Id.IntegerValue;
                if (categoryId == (int)BuiltInCategory.OST_Sprinklers) return "Sprinkler";
            }
            foreach (Element element in elements)
            {
                if (element.Category == null) continue;
                int categoryId = element.Category.Id.IntegerValue;
                if (categoryId == (int)BuiltInCategory.OST_PipeCurves ||
                    categoryId == (int)BuiltInCategory.OST_PipeFittings ||
                    categoryId == (int)BuiltInCategory.OST_PipeInsulations)
                {
                    return "Pipe";
                }
            }
            return "All";
        }

        private static bool MatchesScope(string ruleScope, string dimensionScope)
        {
            return string.Equals(ruleScope, "All", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(ruleScope, dimensionScope, StringComparison.OrdinalIgnoreCase);
        }

        private static string FormatMeters(double meters)
        {
            return meters.ToString("0.###");
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

        private static void SortIssues(AuditResult result)
        {
            result.Issues.Sort((a, b) =>
            {
                int comparison = Rank(a.Severity).CompareTo(Rank(b.Severity));
                if (comparison != 0) return comparison;
                comparison = a.Type.CompareTo(b.Type);
                if (comparison != 0) return comparison;
                return string.Compare(a.ElementLabel, b.ElementLabel, StringComparison.Ordinal);
            });
        }

        private static int Rank(IssueSeverity severity)
        {
            if (severity == IssueSeverity.Error) return 0;
            if (severity == IssueSeverity.Warning) return 1;
            return 2;
        }
    }
}
