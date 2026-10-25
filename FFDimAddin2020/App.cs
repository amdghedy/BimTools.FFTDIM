using System;
using System.Reflection;
using Autodesk.Revit.UI;

namespace BimTools.FFTDIM
{
    /// <summary>
    /// FFTDIM application entry point. Creates the "FF Tools" ribbon tab with three panels:
    ///   Dimension QA  — Audit Dimensions, Clean Redundant, Coverage Check, Tolerance Check
    ///   Quick Dimension — Spacing Chain, Clearance
    ///   Output        — Export Report
    /// </summary>
    public class App : IExternalApplication
    {
        public const string Brand = "FFTDIM";
        private const string TabName = "FF Tools";

        public Result OnStartup(UIControlledApplication application)
        {
            try
            {
                try
                {
                    application.CreateRibbonTab(TabName);
                }
                catch (ArgumentException)
                {
                    // Tab already exists (another FFTDIM session or a hot-reload); reuse it.
                }

                string assemblyPath = Assembly.GetExecutingAssembly().Location;

                RibbonPanel qaPanel = GetOrCreatePanel(application, "Dimension QA");
                if (qaPanel != null)
                {
                    AddButton(qaPanel, "Audit\nDimensions", "AuditDimensions",
                        "BimTools.FFTDIM.Commands.AuditDimensionsCommand", assemblyPath,
                        "Scan the active view for broken, zero-value, orphaned and duplicate dimensions.",
                        "Read-only check. Reports zero-value dimensions, duplicate reference sets, " +
                        "orphaned references and zero-length chain segments. Nothing is modified.");
                    AddButton(qaPanel, "Clean\nRedundant", "CleanRedundant",
                        "BimTools.FFTDIM.Commands.CleanRedundantCommand", assemblyPath,
                        "Delete duplicate and zero-value dimensions in the active view.",
                        "Finds dimensions that reference the exact same geometry as another dimension, " +
                        "plus zero-value dimensions, shows a confirmation, then deletes them inside a " +
                        "single transaction (undoable with Ctrl+Z).");
                    AddButton(qaPanel, "Coverage\nCheck", "CoverageCheck",
                        "BimTools.FFTDIM.Commands.CoverageCheckCommand", assemblyPath,
                        "Find fire-fighting elements that no dimension references in this view.",
                        "Lists every sprinkler, fire pipe, fitting or fire equipment item in the active " +
                        "view that is not referenced by any dimension. Read-only.");
                    AddButton(qaPanel, "Tolerance\nCheck", "ToleranceCheck",
                        "BimTools.FFTDIM.Commands.ToleranceCheckCommand", assemblyPath,
                        "Check dimension values against user-configured tolerance rules.",
                        "Edit rules (scope, min/max in metres), then scan the view. Values below a " +
                        "minimum are errors, values above a maximum are warnings. Rules persist in " +
                        "%APPDATA%\\BimTools\\FFTDIM\\tolerance_rules.json.");
                }

                RibbonPanel quickPanel = GetOrCreatePanel(application, "Quick Dimension");
                if (quickPanel != null)
                {
                    AddButton(quickPanel, "Spacing\nChain", "SpacingDimension",
                        "BimTools.FFTDIM.Commands.SpacingDimensionCommand", assemblyPath,
                        "Create a spacing chain dimension across selected sprinklers / point fittings.",
                        "Select two or more point-based fire-fighting families in a plan view. FFTDIM " +
                        "orders them along the dominant view axis and creates one multi-segment " +
                        "dimension using their centre reference planes.");
                    AddButton(quickPanel, "Clearance", "ClearanceDimension",
                        "BimTools.FFTDIM.Commands.ClearanceDimensionCommand", assemblyPath,
                        "Dimension selected fire-fighting elements to the nearest wall face or grid.",
                        "Select one or more sprinklers / point fittings in a plan view. For each element " +
                        "FFTDIM finds the nearest grid line or wall in the view and creates a clearance " +
                        "dimension from the element centre to that target.");
                }

                RibbonPanel outputPanel = GetOrCreatePanel(application, "Output");
                if (outputPanel != null)
                {
                    AddButton(outputPanel, "Export\nReport", "ExportReport",
                        "BimTools.FFTDIM.Commands.ExportReportCommand", assemblyPath,
                        "Run a full audit of the active view and save it as CSV or HTML.",
                        "Runs the same audit as 'Audit Dimensions' and writes the findings to a " +
                        "timestamped file of your choice.");
                }

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                TaskDialog.Show(Brand, "FFTDIM failed to start:\n\n" + ex.Message);
                return Result.Failed;
            }
        }

        public Result OnShutdown(UIControlledApplication application)
        {
            return Result.Succeeded;
        }

        private static RibbonPanel GetOrCreatePanel(UIControlledApplication application, string panelName)
        {
            try
            {
                return application.CreateRibbonPanel(TabName, panelName);
            }
            catch (ArgumentException)
            {
                try
                {
                    foreach (RibbonPanel panel in application.GetRibbonPanels(TabName))
                    {
                        if (panel.Name == panelName) return panel;
                    }
                }
                catch
                {
                    // Fall through.
                }
                return null;
            }
        }

        private static void AddButton(RibbonPanel panel, string text, string name, string className,
            string assemblyPath, string tooltip, string longDescription)
        {
            var data = new PushButtonData(name, text, assemblyPath, className)
            {
                ToolTip = tooltip,
                LongDescription = longDescription,
                AvailabilityClassName = null
            };
            panel.AddItem(data);
        }
    }
}
