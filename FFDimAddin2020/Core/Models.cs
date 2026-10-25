using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace BimTools.FFTDIM.Core
{
    /// <summary>Categories of findings the audit can report.</summary>
    public enum DimensionIssueType
    {
        /// <summary>Dimension measures (almost) zero — usually broken or mis-snapped references.</summary>
        ZeroValue,

        /// <summary>Dimension shares the exact same reference set as another dimension.</summary>
        Duplicate,

        /// <summary>One of the dimension references no longer resolves to a model element.</summary>
        OrphanReference,

        /// <summary>A chain (multi-segment) dimension contains a zero-length segment.</summary>
        SegmentZero,

        /// <summary>Dimension value falls outside a configured tolerance rule.</summary>
        ToleranceViolation,

        /// <summary>Fire-fighting element exists in the view but no dimension references it.</summary>
        CoverageGap,

        /// <summary>At least one reference of the dimension resolves into a linked Revit model;
        /// carries the resolved linked-element description when produced by FFD-10.</summary>
        LinkedReference,

        /// <summary>FFD-10: a reference points into a linked model, but the element it names
        /// no longer exists inside the linked document (broken cross-link reference).</summary>
        LinkedElementMissing,

        /// <summary>FFD-10: a fire-fighting element living inside a linked model is not
        /// referenced by any dimension of the active view.</summary>
        LinkedCoverageGap,

        /// <summary>FFD-10: informational record about one linked model (inventory/summary).</summary>
        LinkedModelInfo,

        /// <summary>FFD-10: the linked model is unloaded; its dimensions cannot be resolved.</summary>
        LinkedModelUnloaded
    }

    /// <summary>Severity levels used for reporting and sorting.</summary>
    public enum IssueSeverity
    {
        Info,
        Warning,
        Error
    }

    /// <summary>A single finding produced by an FFTDIM audit. Bound to the WPF results grid,
    /// therefore every member is a property (WPF binding does not resolve public fields).</summary>
    public class DimensionIssue
    {
        public DimensionIssueType Type { get; set; }
        public IssueSeverity Severity { get; set; }

        /// <summary>Element to highlight when the user clicks "Select in model".</summary>
        public ElementId ElementId { get; set; }

        /// <summary>Human readable label of the owning element, e.g. "Dimension 1234567".</summary>
        public string ElementLabel { get; set; }

        /// <summary>Formatted measurement in project display units (already resolved).</summary>
        public string ValueText { get; set; }

        public string ViewName { get; set; }
        public string Detail { get; set; }

        /// <summary>True when the command considers the element safe to delete (duplicates, zero-value).</summary>
        public bool CanDelete { get; set; }
    }

    /// <summary>Describes one fire-fighting element found in a view.</summary>
    public class FireElementInfo
    {
        public ElementId Id { get; set; }
        public string Name { get; set; }
        public string Category { get; set; }
        public string Family { get; set; }
        public string Type { get; set; }
        public string System { get; set; }
        public bool IsDimensioned { get; set; }

        public override string ToString()
        {
            return string.Format("{0} '{1}' (id {2})", Category ?? "Element", Name, Id != null ? Id.IntegerValue.ToString() : "?");
        }
    }

    /// <summary>Aggregate result of one audit run. Displayed by the results window and exported by ReportExporter.</summary>
    public class AuditResult
    {
        public string DocumentTitle { get; set; }
        public string ViewName { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public int TotalDimensions { get; set; }
        public int TotalFireElements { get; set; }
        public int CoveredFireElements { get; set; }

        /// <summary>Number of linked Revit models involved in the audit (FFD-10); 0 for the other commands.</summary>
        public int LinkedModelCount { get; set; }

        public List<DimensionIssue> Issues { get; } = new List<DimensionIssue>();

        public int CountBySeverity(IssueSeverity severity)
        {
            int count = 0;
            foreach (DimensionIssue issue in Issues)
            {
                if (issue.Severity == severity) count++;
            }
            return count;
        }

        public string SummaryText
        {
            get
            {
                string summary = string.Format(
                    "{0} dimensions scanned · {1} fire-fighting elements ({2} covered) · {3} findings ({4} error, {5} warning, {6} info)",
                    TotalDimensions,
                    TotalFireElements,
                    CoveredFireElements,
                    Issues.Count,
                    CountBySeverity(IssueSeverity.Error),
                    CountBySeverity(IssueSeverity.Warning),
                    CountBySeverity(IssueSeverity.Info));

                if (LinkedModelCount > 0)
                {
                    summary += string.Format(" · {0} linked model{1}", LinkedModelCount, LinkedModelCount == 1 ? "" : "s");
                }
                return summary;
            }
        }
    }
}
