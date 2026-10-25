using System.Collections.Generic;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BimTools.FFTDIM.Core;
using BimTools.FFTDIM.UI;

namespace BimTools.FFTDIM.Commands
{
    /// <summary>
    /// FFD-09 · Batch Audit Views (read-only).
    /// Lets the user tick a set of views (plans, sections, elevations, drafting, 3D),
    /// then runs the standard dimension audit (FFD-01 semantics) on every selected view
    /// and merges the findings into one results window. Each finding is tagged with the
    /// view it came from, and the merged result exports to CSV / HTML / JSON as usual.
    /// No transaction is opened — nothing is modified.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class BatchAuditCommand : CommandBase
    {
        protected override Result OnExecute(ExternalCommandData commandData, UIDocument uidoc, ref string message)
        {
            Document doc = uidoc.Document;

            AppSettings settings = AppSettingsStore.LoadOrDefaults();
            L.Apply(settings);

            List<View> views = BatchAuditor.CollectAuditableViews(doc);
            if (views.Count == 0)
            {
                TaskDialog.Show(App.Brand, "This document contains no auditable views " +
                                           "(plans, sections, elevations, drafting or 3D).");
                return Result.Cancelled;
            }

            var picker = new ViewPickerWindow(views);
            if (picker.ShowDialog() != true) return Result.Cancelled;

            List<View> selected = picker.SelectedViews;
            if (selected.Count == 0) return Result.Cancelled;

            AuditResult result = BatchAuditor.Run(doc, selected, settings);

            if (result.Issues.Count == 0)
            {
                TaskDialog.Show(App.Brand, string.Format(
                    "No dimension issues found across {0} view{1}.\n\n{2} dimensions scanned.",
                    selected.Count, selected.Count == 1 ? "" : "s", result.TotalDimensions));
                return Result.Succeeded;
            }

            new AuditResultsWindow(result, uidoc).ShowDialog();
            return Result.Succeeded;
        }
    }
}
