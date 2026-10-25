using System;
using System.Reflection;
using Autodesk.Revit.UI;
using BimTools.FFTDIM.Core;

namespace BimTools.FFTDIM
{
    /// <summary>
    /// FFTDIM application entry point. Creates the "FF Tools" ribbon tab with three panels:
    ///   Dimension QA  — Audit Dimensions, Clean Redundant, Coverage Check, Tolerance Check, Batch Audit Views, Linked Model Audit
    ///   Quick Dimension — Spacing Chain, Clearance
    ///   Output        — Export Report, BCF Export, BCF Import, Settings &amp; Options
    /// Panel and button captions localise through <see cref="L"/>; the language comes from
    /// %APPDATA%\BimTools\FFTDIM\settings.json and applies on the next Revit start.
    /// </summary>
    public class App : IExternalApplication
    {
        public const string Brand = "FFTDIM";
        private const string TabName = "FF Tools";

        public Result OnStartup(UIControlledApplication application)
        {
            try
            {
                // Load persisted options before building the ribbon so captions follow the
                // saved interface language immediately on this session's first start.
                AppSettings settings = AppSettingsStore.LoadOrDefaults();
                L.Apply(settings);

                try
                {
                    application.CreateRibbonTab(TabName);
                }
                catch (ArgumentException)
                {
                    // Tab already exists (another FFTDIM session or a hot-reload); reuse it.
                }

                string assemblyPath = Assembly.GetExecutingAssembly().Location;

                RibbonPanel qaPanel = GetOrCreatePanel(application, L.T("PanelQa"));
                if (qaPanel != null)
                {
                    AddButton(qaPanel, L.T("BtnAudit"), "AuditDimensions",
                        "BimTools.FFTDIM.Commands.AuditDimensionsCommand", assemblyPath,
                        "Scan the active view for broken, zero-value, orphaned and duplicate dimensions.",
                        "Read-only check. Reports zero-value dimensions, duplicate reference sets, " +
                        "orphaned references and zero-length chain segments. Nothing is modified. " +
                        "Thresholds and spot-dimension/linked-model switches live in Settings & Options.");
                    AddButton(qaPanel, L.T("BtnClean"), "CleanRedundant",
                        "BimTools.FFTDIM.Commands.CleanRedundantCommand", assemblyPath,
                        "Delete duplicate and zero-value dimensions in the active view.",
                        "Finds dimensions that reference the exact same geometry as another dimension, " +
                        "plus zero-value dimensions, shows a confirmation, then deletes them inside a " +
                        "single transaction (undoable with Ctrl+Z). The zero-value threshold comes from " +
                        "Settings & Options.");
                    AddButton(qaPanel, L.T("BtnCoverage"), "CoverageCheck",
                        "BimTools.FFTDIM.Commands.CoverageCheckCommand", assemblyPath,
                        "Find fire-fighting elements that no dimension references in this view.",
                        "Lists every sprinkler, fire pipe, fitting or fire equipment item in the active " +
                        "view that is not referenced by any dimension. Read-only.");
                    AddButton(qaPanel, L.T("BtnTolerance"), "ToleranceCheck",
                        "BimTools.FFTDIM.Commands.ToleranceCheckCommand", assemblyPath,
                        "Check dimension values against user-configured tolerance rules.",
                        "Edit rules (scope, min/max in metres), then scan the view. Values below a " +
                        "minimum are errors, values above a maximum are warnings. Rules persist in " +
                        "%APPDATA%\\BimTools\\FFTDIM\\tolerance_rules.json.");
                    AddButton(qaPanel, L.T("BtnBatch"), "BatchAuditViews",
                        "BimTools.FFTDIM.Commands.BatchAuditCommand", assemblyPath,
                        "Audit several views in one run and merge the findings.",
                        "Pick any number of plans, sections, elevations, drafting or 3D views, then run " +
                        "the standard dimension audit on each and review the merged results. Every " +
                        "finding is tagged with its view. Read-only.");
                    AddButton(qaPanel, L.T("BtnLinkedAudit"), "LinkedModelAudit",
                        "BimTools.FFTDIM.Commands.LinkedModelAuditCommand", assemblyPath,
                        "Deep audit of dimensions that reference linked Revit models.",
                        "Inventories every Revit link (load status, fire-element count), resolves each " +
                        "cross-link dimension reference down to the element inside the linked document, " +
                        "reports broken cross-link references, zero-value dimensions and duplicates, and " +
                        "checks fire-fighting coverage inside each loaded link (switchable in Settings " +
                        "& Options). Read-only.");
                }

                RibbonPanel quickPanel = GetOrCreatePanel(application, L.T("PanelQuick"));
                if (quickPanel != null)
                {
                    AddButton(quickPanel, L.T("BtnSpacing"), "SpacingDimension",
                        "BimTools.FFTDIM.Commands.SpacingDimensionCommand", assemblyPath,
                        "Create a spacing chain dimension across selected sprinklers / point fittings.",
                        "Select two or more point-based fire-fighting families in a plan view. FFTDIM " +
                        "orders them along the dominant view axis and creates one multi-segment " +
                        "dimension using their centre reference planes. Offset and end extension are " +
                        "configurable in Settings & Options.");
                    AddButton(quickPanel, L.T("BtnClearance"), "ClearanceDimension",
                        "BimTools.FFTDIM.Commands.ClearanceDimensionCommand", assemblyPath,
                        "Dimension selected fire-fighting elements to the nearest wall face or grid.",
                        "Select sprinklers, point fittings or linear fire pipes in a plan view. For each " +
                        "element FFTDIM finds the nearest grid line or wall within the configured search " +
                        "radius and creates a clearance dimension. Thresholds and the walls-over-grids " +
                        "preference live in Settings & Options.");
                }

                RibbonPanel outputPanel = GetOrCreatePanel(application, L.T("PanelOutput"));
                if (outputPanel != null)
                {
                    AddButton(outputPanel, L.T("BtnExport"), "ExportReport",
                        "BimTools.FFTDIM.Commands.ExportReportCommand", assemblyPath,
                        "Run a full audit of the active view and save it as CSV, HTML or JSON.",
                        "Runs the same audit as 'Audit Dimensions' and writes the findings to a " +
                        "timestamped file of your choice. The format pre-selected in Settings & Options " +
                        "is marked as the default.");
                    AddButton(outputPanel, L.T("BtnBcfExport"), "BcfExport",
                        "BimTools.FFTDIM.Commands.BcfExportCommand", assemblyPath,
                        "Export the audit findings as a BCF 2.1 issue package.",
                        "Audits the active view and writes a .bcf package readable by BIMcollab, Revizto, " +
                        "Autodesk Construction Cloud, Solibri and Trimble Connect. Each finding becomes a " +
                        "topic (or all findings one merged topic, per Settings & Options) with a viewpoint " +
                        "camera and the involved Revit elements as components. Read-only.");
                    AddButton(outputPanel, L.T("BtnBcfImport"), "BcfImport",
                        "BimTools.FFTDIM.Commands.BcfImportCommand", assemblyPath,
                        "Import a BCF 2.1 package and locate its components in this model.",
                        "Lists the topics of a .bcf / .bcfzip file (from 'BCF Export' or any BCF platform), " +
                        "resolves the referenced Revit element ids against the current model and lets you " +
                        "select the resolved elements of each topic for re-verification. Read-only.");
                    AddButton(outputPanel, L.T("BtnSettings"), "SettingsOptions",
                        "BimTools.FFTDIM.Commands.SettingsCommand", assemblyPath,
                        "Configure thresholds, quick-dimension geometry, linked-model and BCF options, report format and language.",
                        "Edits %APPDATA%\\BimTools\\FFTDIM\\settings.json: zero-value threshold, spot " +
                        "dimensions, linked-model flagging, linked-model coverage checking, BCF topic " +
                        "granularity and viewpoint cameras, spacing-chain offset/extension, clearance " +
                        "skip threshold and search radius, walls-over-grids preference, default report " +
                        "format and the interface language (English / Arabic).");
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
