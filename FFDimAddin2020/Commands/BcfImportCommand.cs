using System;
using System.Collections.Generic;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BimTools.FFTDIM.Core;
using BimTools.FFTDIM.UI;

namespace BimTools.FFTDIM.Commands
{
    /// <summary>
    /// FFD-12 · BCF Import (read-only).
    /// Opens a BCF 2.1 package (e.g. one produced by 'BCF Export', or any topic list from
    /// BIMcollab / Revizto / ACC / Solibri / Trimble Connect), lists its topics in a preview
    /// window and resolves the referenced components against the current model. The user can
    /// select the resolved elements of any topic directly in the model — typically to
    /// re-verify the geometry an external QA comment refers to. No transaction is opened.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class BcfImportCommand : CommandBase
    {
        protected override Result OnExecute(ExternalCommandData commandData, UIDocument uidoc, ref string message)
        {
            Document doc = uidoc.Document;

            AppSettings settings = AppSettingsStore.LoadOrDefaults();
            L.Apply(settings);

            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "FFTDIM — import a BCF 2.1 package",
                Filter = "BCF files (*.bcf;*.bcfzip)|*.bcf;*.bcfzip|All files (*.*)|*.*",
                FilterIndex = 1,
                CheckFileExists = true
            };
            if (dialog.ShowDialog() != true) return Result.Cancelled;

            List<BcfTopicRecord> topics;
            try
            {
                topics = BcfImporter.Read(dialog.FileName);
            }
            catch (InvalidDataException ex)
            {
                TaskDialog.Show(App.Brand, "Not a readable BCF package:\n\n" + ex.Message);
                return Result.Cancelled;
            }
            catch (Exception ex)
            {
                TaskDialog.Show(App.Brand, "Could not open the BCF package:\n\n" + ex.Message +
                    "\n\nMake sure the file is not open in another application.");
                return Result.Failed;
            }

            if (topics.Count == 0)
            {
                TaskDialog.Show(App.Brand, "The BCF package contains no topics.");
                return Result.Succeeded;
            }

            foreach (BcfTopicRecord topic in topics)
            {
                topic.ResolveIds(doc);
            }

            new BcfPreviewWindow(topics, dialog.FileName, uidoc).ShowDialog();
            return Result.Succeeded;
        }
    }
}
