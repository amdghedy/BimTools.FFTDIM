using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BimTools.FFTDIM.Core;
using BimTools.FFTDIM.UI;

namespace BimTools.FFTDIM.Commands
{
    /// <summary>
    /// FFD-01 · Audit Dimensions (read-only).
    /// Scans every dimension in the active view and reports:
    ///   · zero-value dimensions (broken / mis-snapped references),
    ///   · duplicate dimensions (identical reference sets),
    ///   · orphaned references,
    ///   · zero-length segments inside chain dimensions,
    ///   · linked-model references (when enabled in Settings & Options).
    /// Spot dimensions are included only when opted in via Settings & Options.
    /// Findings open in the results window; nothing in the model is modified.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class AuditDimensionsCommand : CommandBase
    {
        protected override Result OnExecute(ExternalCommandData commandData, UIDocument uidoc, ref string message)
        {
            Document doc = uidoc.Document;
            View view = uidoc.ActiveView;

            string reject;
            if (!ViewGuard.IsGraphicalView(view, out reject))
            {
                TaskDialog.Show(App.Brand, reject);
                return Result.Cancelled;
            }

            AppSettings settings = AppSettingsStore.LoadOrDefaults();
            L.Apply(settings);

            AuditResult result = DimensionAuditor.Audit(doc, view, settings);
            if (result.Issues.Count == 0)
            {
                TaskDialog.Show(App.Brand, string.Format(
                    "No dimension issues found in view '{0}'.\n\n{1} dimensions scanned.",
                    view.Name, result.TotalDimensions));
                return Result.Succeeded;
            }

            new AuditResultsWindow(result, uidoc).ShowDialog();
            return Result.Succeeded;
        }
    }
}
