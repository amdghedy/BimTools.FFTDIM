using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BimTools.FFTDIM.Core;

namespace BimTools.FFTDIM.Commands
{
    /// <summary>
    /// FFD-07 · Export Report (read-only).
    /// Runs the full dimension audit of the active view and writes the findings to a
    /// timestamped CSV (Excel-friendly, UTF-8 BOM) or HTML file chosen by the user.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class ExportReportCommand : CommandBase
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

            var chooser = new TaskDialog(App.Brand)
            {
                MainInstruction = "Export dimension audit report",
                Content = string.Format("FFTDIM will audit view '{0}' and save the findings.\nChoose the report format:", view.Name)
            };
            chooser.AddCommandLink(TaskDialogCommandLinkId.CommandLink1, "CSV file (opens in Excel)");
            chooser.AddCommandLink(TaskDialogCommandLinkId.CommandLink2, "HTML page (opens in any browser)");

            TaskDialogResult choice = chooser.Show();
            if (choice != TaskDialogResult.CommandLink1 && choice != TaskDialogResult.CommandLink2)
            {
                return Result.Cancelled;
            }
            bool asCsv = choice == TaskDialogResult.CommandLink1;

            AuditResult result = DimensionAuditor.Audit(doc, view);

            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Title = "FFTDIM — save audit report",
                Filter = asCsv ? "CSV files (*.csv)|*.csv" : "HTML files (*.html)|*.html",
                FileName = string.Format("FFTDIM_Audit_{0:yyyyMMdd_HHmm}.{1}", DateTime.Now, asCsv ? "csv" : "html")
            };
            if (dialog.ShowDialog() != true) return Result.Cancelled;

            try
            {
                string path = ReportExporter.Write(dialog.FileName, result);

                var confirm = new TaskDialog(App.Brand)
                {
                    MainInstruction = "Report saved",
                    Content = path + "\n\nOpen the file now?"
                };
                confirm.CommonButtons = TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.No;
                if (confirm.Show() == TaskDialogResult.Yes)
                {
                    try
                    {
                        System.Diagnostics.Process.Start(path);
                    }
                    catch
                    {
                        // No default viewer for the extension; the path is already shown.
                    }
                }
            }
            catch (Exception ex)
            {
                TaskDialog.Show(App.Brand, "Could not write the report:\n\n" + ex.Message);
                return Result.Failed;
            }

            return Result.Succeeded;
        }
    }
}
