using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BimTools.FFTDIM.Core;

namespace BimTools.FFTDIM.Commands
{
    /// <summary>
    /// FFD-11 · BCF Export (read-only).
    /// Runs the standard dimension audit of the active view and writes the findings as a
    /// BCF 2.1 package for issue round-trips with BIMcollab, Revizto, Autodesk Construction
    /// Cloud, Solibri, Trimble Connect and other BCF-capable platforms. Topic granularity
    /// (one topic per finding vs one merged topic) and the embedded viewpoint camera are
    /// controlled from Settings &amp; Options. No transaction is opened.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class BcfExportCommand : CommandBase
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
                    "No findings to export — the audit of view '{0}' came back clean.\n\n" +
                    "BCF topics are created from audit findings; run a command that reports issues first.",
                    view.Name));
                return Result.Succeeded;
            }

            string topicMode = settings.BcfTopicPerIssue
                ? "one topic per finding (" + result.Issues.Count + " topics)"
                : "one merged topic (all " + result.Issues.Count + " findings)";
            string cameraMode = settings.BcfIncludeCamera
                ? "viewpoint camera included"
                : "no viewpoint camera";

            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Title = "FFTDIM — export audit findings as BCF 2.1",
                Filter = "BCF 2.1 files (*.bcf)|*.bcf|BCF zip files (*.bcfzip)|*.bcfzip",
                FilterIndex = 1,
                FileName = string.Format("FFTDIM_Audit_{0:yyyyMMdd_HHmm}.bcf", DateTime.Now)
            };
            if (dialog.ShowDialog() != true) return Result.Cancelled;

            try
            {
                string path = BcfExporter.Write(dialog.FileName, result, view, settings);

                var confirm = new TaskDialog(App.Brand)
                {
                    MainInstruction = "BCF export saved",
                    Content = path + "\n\n" + topicMode + ", " + cameraMode + ".\n\n" +
                              "Import this file into your BCF platform (BIMcollab, Revizto, ACC, Solibri, ...) " +
                              "or back into FFTDIM via 'BCF Import'.\n\nLocate the file in Explorer now?"
                };
                confirm.CommonButtons = TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.No;
                if (confirm.Show() == TaskDialogResult.Yes)
                {
                    try
                    {
                        System.Diagnostics.Process.Start("explorer.exe", "/select,\"" + path + "\"");
                    }
                    catch
                    {
                        // Explorer not available (unexpected on Revit workstations); path already shown.
                    }
                }
            }
            catch (Exception ex)
            {
                TaskDialog.Show(App.Brand, "Could not write the BCF package:\n\n" + ex.Message +
                    "\n\nThe file may be open in another application, or the folder is read-only.");
                return Result.Failed;
            }

            return Result.Succeeded;
        }
    }
}
