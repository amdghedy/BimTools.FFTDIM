using System.Collections.Generic;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BimTools.FFTDIM.Core;
using BimTools.FFTDIM.UI;

namespace BimTools.FFTDIM.Commands
{
    /// <summary>
    /// FFD-04 · Tolerance Check (read-only check, model-changing only for settings).
    /// Opens the tolerance rules editor, then scans the active view:
    ///   · value below a rule's minimum → Error,
    ///   · value above a rule's maximum → Warning.
    /// Only dimensions referencing elements matching the rule scope are checked
    /// (Sprinkler / Pipe / All). Rules persist in %APPDATA%\BimTools\FFTDIM\tolerance_rules.json.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class ToleranceCheckCommand : CommandBase
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

            List<ToleranceRule> rules = ToleranceRuleStore.LoadOrDefaults();

            var settings = new ToleranceSettingsWindow(rules);
            bool? accepted = settings.ShowDialog();
            if (accepted != true) return Result.Cancelled;

            ToleranceRuleStore.Save(rules);

            if (!ToleranceRuleStore.HasEnabledRules(rules))
            {
                TaskDialog.Show(App.Brand, "No enabled rules with a configured limit. Enable at least one rule " +
                                           "and set a minimum or maximum, then run the check again.");
                return Result.Succeeded;
            }

            AuditResult result = DimensionAuditor.ToleranceAudit(doc, view, rules);
            if (result.Issues.Count == 0)
            {
                TaskDialog.Show(App.Brand, string.Format(
                    "All checked dimension values comply with the configured rules.\n\n{0} dimensions scanned in view '{1}'.",
                    result.TotalDimensions, view.Name));
                return Result.Succeeded;
            }

            new AuditResultsWindow(result, uidoc).ShowDialog();
            return Result.Succeeded;
        }
    }
}
