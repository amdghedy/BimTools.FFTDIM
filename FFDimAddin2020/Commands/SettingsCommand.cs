using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BimTools.FFTDIM.Core;
using BimTools.FFTDIM.UI;

namespace BimTools.FFTDIM.Commands
{
    /// <summary>
    /// FFD-08 · Settings &amp; Options (no model changes).
    /// Opens the options dialog that edits %APPDATA%\BimTools\FFTDIM\settings.json:
    /// audit thresholds, spot-dimension and linked-model switches, spacing-chain and
    /// clearance geometry, default report format, and the interface language.
    /// Unlike the other commands this one deliberately does NOT require an open
    /// document — it configures the add-in, not the model — so it does not derive
    /// from CommandBase.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class SettingsCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                AppSettings settings = AppSettingsStore.LoadOrDefaults();
                L.Apply(settings);
                new SettingsWindow(settings).ShowDialog();
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message + Environment.NewLine + ex.StackTrace;
                return Result.Failed;
            }
        }
    }
}
