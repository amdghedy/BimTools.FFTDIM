using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BimTools.FFTDIM.Core;

namespace BimTools.FFTDIM.Commands
{
    /// <summary>
    /// FFD-02 · Clean Redundant (model-changing).
    /// Deletes two categories of dimension in the active view:
    ///   · duplicates — dimensions sharing the exact reference set of another dimension;
    ///   · zero-value dimensions — almost always broken references.
    /// Within each duplicate group the dimension with the lowest element id is kept.
    /// A confirmation dialog lists the counts before any deletion; the operation runs in a
    /// single transaction and can be undone with Ctrl+Z.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class CleanRedundantCommand : CommandBase
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

            IList<Dimension> dimensions = DimensionScanner.Collect(doc, view.Id, settings.IncludeSpotDimensions);
            double zeroToleranceFeet = settings.ZeroValueThresholdFeet;

            var toDelete = new List<ElementId>();
            int duplicateCount = 0;
            int zeroCount = 0;

            foreach (List<Dimension> group in RedundancyFinder.FindDuplicateGroups(dimensions))
            {
                group.Sort((a, b) => a.Id.IntegerValue.CompareTo(b.Id.IntegerValue));
                for (int i = 1; i < group.Count; i++)
                {
                    toDelete.Add(group[i].Id);
                    duplicateCount++;
                }
            }

            foreach (Dimension dim in dimensions)
            {
                if (!dim.Value.HasValue) continue;
                if (Math.Abs(dim.Value.Value) >= zeroToleranceFeet) continue;
                if (toDelete.Contains(dim.Id)) continue;
                toDelete.Add(dim.Id);
                zeroCount++;
            }

            if (toDelete.Count == 0)
            {
                TaskDialog.Show(App.Brand, string.Format(
                    "No redundant or zero-value dimensions found in view '{0}'.\n\n{1} dimensions scanned.",
                    view.Name, dimensions.Count));
                return Result.Succeeded;
            }

            var confirm = new TaskDialog(App.Brand)
            {
                MainInstruction = string.Format("Delete {0} dimension{1}?",
                    toDelete.Count, toDelete.Count == 1 ? string.Empty : "s"),
                Content = string.Format(
                    "{0} duplicate and {1} zero-value dimension{2} will be deleted from view '{3}'.\n\n" +
                    "The operation is a single transaction — undo it with Ctrl+Z if anything looks wrong.",
                    duplicateCount, zeroCount, zeroCount == 1 ? string.Empty : "s", view.Name)
            };
            confirm.CommonButtons = TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.No;
            if (confirm.Show() != TaskDialogResult.Yes) return Result.Cancelled;

            int deletedCount;
            using (Transaction transaction = new Transaction(doc, "FFTDIM: Clean redundant dimensions"))
            {
                transaction.Start();
                ICollection<ElementId> removed = doc.Delete(toDelete);
                deletedCount = removed != null ? removed.Count : 0;
                transaction.Commit();
            }

            TaskDialog.Show(App.Brand, string.Format("Deleted {0} element{1}.",
                deletedCount, deletedCount == 1 ? string.Empty : "s"));
            return Result.Succeeded;
        }
    }
}
