using System.Collections.Generic;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace FFDimAddin2020
{
    [Transaction(TransactionMode.Manual)]
    public class FireDimCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            SettingsStore.Load(); // user settings from last session / Settings dialog

            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            if (uidoc == null) { message = "No active document."; return Result.Failed; }
            Document doc = uidoc.Document;
            View view = doc.ActiveView;

            string reason;
            if (!PlanViewValidator.IsUsableFloorPlan(view, out reason))
            {
                TaskDialog.Show("Fire Dim", reason + "\nOpen a 2D Floor Plan with Pipes visible and run again.");
                return Result.Failed;
            }

            List<PipeInfo> pipes = FirePipeCollector.Collect(doc, view);
            if (pipes.Count == 0)
            {
                TaskDialog.Show("Fire Dim",
                    "No firefighting pipes found in this view.\n" +
                    "Check Settings (filter mode / keywords), or set 'All pipes in view' for testing.");
                return Result.Succeeded;
            }

            FireDimensionBuilder.ResetDedupe();
            FireDimensionBuilder.FailedRun = 0;
            FireDimensionBuilder.FailedWall = 0;

            int runDims = 0, wallDims = 0;
            int wallFaces = 0;

            using (Transaction tx = new Transaction(doc, "Fire Dim"))
            {
                tx.Start();

                List<List<PipeInfo>> runs = PipeRunGrouper.Group(pipes);
                List<XYZ> joints = CenterlineJoints.Compute(pipes, UnitHelper.MmToFt(FireDimSettings.MaxFittingGapMm));
                List<EndFace> walls = WallFaceIndex.Build(doc, view);
                wallFaces = walls.Count;

                TickCache ticks = TickCache.Build(doc, view, joints);

                if (FireDimSettings.CreateRunDimensions)
                    runDims = FireDimensionBuilder.BuildRunDimensions(doc, view, runs, joints, walls, ticks);

                if (FireDimSettings.CreateWallDimensions)
                    wallDims = FireDimensionBuilder.BuildWallDimensions(doc, view, runs, walls, joints, ticks);

                tx.Commit();
            }

            TaskDialog.Show("Fire Dim", string.Format(
                "View: {0}\nFire pipes: {1}\nWall faces found: {2}\n\nRun dims: {3} (failed {4})\nRoute-to-wall dims: {5} (failed {6})\nDuplicates skipped: {7}",
                view.Title, pipes.Count, wallFaces,
                runDims, FireDimensionBuilder.FailedRun,
                wallDims, FireDimensionBuilder.FailedWall,
                FireDimensionBuilder.SkippedDuplicates));
            return Result.Succeeded;
        }
    }
}