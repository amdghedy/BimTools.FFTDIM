using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BimTools.FFTDIM.Core;
using BimTools.FFTDIM.UI;

namespace BimTools.FFTDIM.Commands
{
    /// <summary>
    /// FFD-03 · Coverage Check (read-only).
    /// Finds every fire-fighting element visible in the active view (sprinklers, fire pipes,
    /// fittings, fire equipment) that is NOT referenced by any dimension in that view.
    /// Use it before issuing drawings to catch undimensioned sprinkler layouts.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class CoverageCheckCommand : CommandBase
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

            AuditResult result = DimensionAuditor.CoverageAudit(doc, view);

            if (result.TotalFireElements == 0)
            {
                TaskDialog.Show(App.Brand, string.Format(
                    "No fire-fighting elements were found in view '{0}'.\n\n" +
                    "Make sure sprinklers, fire pipes or fire fittings are placed and visible in this view. " +
                    "Detection uses categories plus system/naming heuristics — see the README.",
                    view.Name));
                return Result.Succeeded;
            }

            if (result.Issues.Count == 0)
            {
                TaskDialog.Show(App.Brand, string.Format(
                    "All {0} fire-fighting elements in view '{1}' are referenced by at least one dimension.",
                    result.TotalFireElements, view.Name));
                return Result.Succeeded;
            }

            new AuditResultsWindow(result, uidoc).ShowDialog();
            return Result.Succeeded;
        }
    }
}
