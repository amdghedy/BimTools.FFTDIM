using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace BimTools.FFTDIM.Core
{
    /// <summary>
    /// Runs the standard dimension audit (FFD-01 semantics) across a set of views and
    /// merges the findings into a single <see cref="AuditResult"/>. Every issue keeps the
    /// name of the view it came from, so the results window and the exported reports show
    /// exactly where each finding lives. The audit is read-only — no transaction is opened.
    /// </summary>
    public static class BatchAuditor
    {
        /// <summary>
        /// Collects every view of the document that can contain model dimensions:
        /// floor/ceiling/structural plans, sections, elevations, drafting views and
        /// 3D views — templates, schedules, sheets, legends and column schedules excluded.
        /// </summary>
        public static List<View> CollectAuditableViews(Document doc)
        {
            var excluded = new[]
            {
                ViewType.Schedule,
                ViewType.DrawingSheet,
                ViewType.ColumnSchedule,
                ViewType.Legend
            };

            return new FilteredElementCollector(doc)
                .OfClass(typeof(View))
                .Cast<View>()
                .Where(v => v != null && !v.IsTemplate && Array.IndexOf(excluded, v.ViewType) < 0)
                .OrderBy(v => v.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        /// <summary>
        /// Audits each view in turn and merges the findings. The returned result carries
        /// the summed dimension count and one issue list sorted by severity.
        /// </summary>
        public static AuditResult Run(Document doc, IEnumerable<View> views, AppSettings settings)
        {
            AppSettings effective = settings ?? new AppSettings();
            List<View> viewList = views.Where(v => v != null).ToList();

            var merged = new AuditResult
            {
                DocumentTitle = doc.Title,
                ViewName = string.Format("Batch ({0} view{1})", viewList.Count, viewList.Count == 1 ? "" : "s")
            };

            foreach (View view in viewList)
            {
                AuditResult one = DimensionAuditor.Audit(doc, view, effective);
                merged.TotalDimensions += one.TotalDimensions;
                foreach (DimensionIssue issue in one.Issues)
                {
                    merged.Issues.Add(issue);
                }
            }

            DimensionAuditor.SortIssues(merged);
            return merged;
        }
    }
}
