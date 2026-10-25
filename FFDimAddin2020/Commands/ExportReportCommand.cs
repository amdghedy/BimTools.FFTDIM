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
    /// timestamped file chosen by the user. Formats: CSV (Excel-friendly, UTF-8 BOM),
    /// HTML (self-contained page) or JSON (machine-readable, for Power BI / CI).
    /// The format pre-selected by Settings &amp; Options is marked as the default.
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

            AppSettings settings = AppSettingsStore.LoadOrDefaults();
            L.Apply(settings);

            string csvDefault = settings.DefaultReportFormat == ReportOutputFormat.Csv ? "   —  default" : "";
            string htmlDefault = settings.DefaultReportFormat == ReportOutputFormat.Html ? "   —  default" : "";
            string jsonDefault = settings.DefaultReportFormat == ReportOutputFormat.Json ? "   —  default" : "";

            var chooser = new TaskDialog(App.Brand)
            {
                MainInstruction = "Export dimension audit report",
                Content = string.Format("FFTDIM will audit view '{0}' and save the findings.\nChoose the report format:", view.Name)
            };
            chooser.AddCommandLink(TaskDialogCommandLinkId.CommandLink1, "CSV file (opens in Excel)" + csvDefault);
            chooser.AddCommandLink(TaskDialogCommandLinkId.CommandLink2, "HTML page (opens in any browser)" + htmlDefault);
            chooser.AddCommandLink(TaskDialogCommandLinkId.CommandLink3, "JSON file (machine-readable, Power BI / CI)" + jsonDefault);

            TaskDialogResult choice = chooser.Show();
            if (choice != TaskDialogResult.CommandLink1 &&
                choice != TaskDialogResult.CommandLink2 &&
                choice != TaskDialogResult.CommandLink3)
            {
                return Result.Cancelled;
            }

            string extension;
            string filter;
            if (choice == TaskDialogResult.CommandLink2)
            {
                extension = "html";
                filter = "HTML files (*.html)|*.html";
            }
            else if (choice == TaskDialogResult.CommandLink3)
            {
                extension = "json";
                filter = "JSON files (*.json)|*.json";
            }
            else
            {
                extension = "csv";
                filter = "CSV files (*.csv)|*.csv";
            }

            AuditResult result = DimensionAuditor.Audit(doc, view, settings);

            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Title = "FFTDIM — save audit report",
                Filter = filter,
                FileName = string.Format("FFTDIM_Audit_{0:yyyyMMdd_HHmm}.{1}", DateTime.Now, extension)
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
