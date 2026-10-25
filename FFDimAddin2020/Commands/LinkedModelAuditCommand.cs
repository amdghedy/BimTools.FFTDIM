using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BimTools.FFTDIM.Core;
using BimTools.FFTDIM.UI;

namespace BimTools.FFTDIM.Commands
{
    /// <summary>
    /// FFD-10 · Linked-Model Audit (read-only).
    /// Deep analysis of dimensions whose references resolve into linked Revit models:
    /// per-link inventory with fire-element counts, resolution of every cross-link
    /// reference down to the element inside the link document, broken cross-link
    /// detection, zero-value/segment checks across the boundary, link-aware duplicate
    /// detection and (optionally, per settings) fire-element coverage inside each
    /// loaded link. Findings are presented in the shared results window and export to
    /// CSV / HTML / JSON / BCF as usual.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class LinkedModelAuditCommand : CommandBase
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

            int linkCount = LinkedModelAuditor.CollectLinks(doc).Count;
            if (linkCount == 0)
            {
                TaskDialog.Show(App.Brand,
                    "This model contains no linked Revit models.\n\n" +
                    "The Linked-Model Audit inventories Revit links, resolves dimensions that measure " +
                    "linked geometry and checks fire-fighting coverage inside each link. Link models via " +
                    "Insert › Link Revit and run the command again.");
                return Result.Cancelled;
            }

            AuditResult result = LinkedModelAuditor.Audit(doc, view, settings);

            if (result.Issues.Count == 0)
            {
                TaskDialog.Show(App.Brand, string.Format(
                    "No linked-model findings in view '{0}'.\n\n{1} dimension(s) scanned across {2} linked model{3}.",
                    view.Name, result.TotalDimensions, linkCount, linkCount == 1 ? "" : "s"));
                return Result.Succeeded;
            }

            new AuditResultsWindow(result, uidoc).ShowDialog();
            return Result.Succeeded;
        }
    }
}
