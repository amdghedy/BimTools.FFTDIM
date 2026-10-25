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
    /// FFD-06 · Clearance (model-changing).
    /// For each selected fire-fighting element in a plan view, finds the nearest grid line
    /// or wall in that view and creates a clearance dimension:
    ///   · point-based families (sprinklers, point fittings) — from the element's centre
    ///     reference to the grid centre line or wall face (interior preferred, exterior fallback);
    ///   · linear fire pipes (MEPCurve) — from the pipe centreline, measured at the middle
    ///     of the run, to the same target types.
    /// Geometry is driven by Settings &amp; Options: the skip-below threshold (default 50 mm),
    /// the nearest-target search radius (default 10 m) and an optional walls-over-grids
    /// preference. All failures are reported per element; the transaction commits whatever
    /// was created successfully.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class ClearanceDimensionCommand : CommandBase
    {
        private const double StubbleFeet = 0.15; // dim line stub away from reference planes

        protected override Result OnExecute(ExternalCommandData commandData, UIDocument uidoc, ref string message)
        {
            Document doc = uidoc.Document;
            View view = uidoc.ActiveView;

            string reject;
            if (!ViewGuard.IsPlanView(view, out reject))
            {
                TaskDialog.Show(App.Brand, reject);
                return Result.Cancelled;
            }

            ICollection<ElementId> selection = uidoc.Selection.GetElementIds();
            if (selection.Count == 0)
            {
                TaskDialog.Show(App.Brand,
                    "Select one or more sprinklers, point fittings or fire pipes in a plan view, then run this " +
                    "command to dimension each element to its nearest grid line or wall.\n\n" +
                    "Thresholds and the walls-over-grids preference are configurable in Settings & Options.");
                return Result.Cancelled;
            }

            AppSettings settings = AppSettingsStore.LoadOrDefaults();
            L.Apply(settings);

            double skipFeet = settings.ClearanceSkipFeet;
            double searchRadiusFeet = settings.ClearanceSearchRadiusFeet;
            bool preferWalls = settings.PreferWallsOverGrids;
            string skipText = settings.ClearanceSkipMm.ToString("0.0#");

            List<Grid> grids = new FilteredElementCollector(doc, view.Id)
                .OfClass(typeof(Grid))
                .Cast<Grid>()
                .ToList();
            List<Wall> walls = new FilteredElementCollector(doc, view.Id)
                .OfCategory(BuiltInCategory.OST_Walls)
                .OfClass(typeof(Wall))
                .Cast<Wall>()
                .ToList();

            var created = new List<ElementId>();
            var skipped = new List<string>();

            using (Transaction transaction = new Transaction(doc, "FFTDIM: Clearance dimensions"))
            {
                transaction.Start();

                foreach (ElementId id in selection)
                {
                    Element element = doc.GetElement(id);
                    if (element == null) continue;
                    string label = (element.Category != null ? element.Category.Name : "Element") + " '" + element.Name + "'";

                    if (!FireFightingFilter.IsFireFighting(element))
                    {
                        skipped.Add(label + " — not recognised as a fire-fighting element");
                        continue;
                    }

                    // ---- Point-based family path -------------------------------------
                    FamilyInstance instance = element as FamilyInstance;
                    LocationPoint location = element.Location as LocationPoint;
                    if (instance != null && location != null)
                    {
                        XYZ point = location.Point;

                        Reference targetReference;
                        Curve targetCurve;
                        string targetDescription;
                        string failure;
                        if (!FindNearestTarget(grids, walls, point, searchRadiusFeet, preferWalls,
                            out targetReference, out targetCurve, out targetDescription, out failure))
                        {
                            skipped.Add(label + " — " + failure);
                            continue;
                        }

                        IntersectionResult projection = targetCurve.Project(point);
                        if (projection == null)
                        {
                            skipped.Add(label + " — cannot project onto " + targetDescription);
                            continue;
                        }
                        XYZ closestPoint = projection.XYZPoint;

                        XYZ direction = new XYZ(closestPoint.X - point.X, closestPoint.Y - point.Y, 0);
                        if (direction.GetLength() < skipFeet)
                        {
                            skipped.Add(label + string.Format(" — sits within {0} mm of {1}; clearance dimension skipped",
                                skipText, targetDescription));
                            continue;
                        }
                        direction = direction.Normalize();

                        XYZ start = point.Add(direction.Multiply(StubbleFeet));
                        XYZ end = closestPoint.Add(direction.Multiply(StubbleFeet));
                        Line dimensionLine;
                        try
                        {
                            dimensionLine = Line.CreateBound(start, end);
                        }
                        catch (Exception)
                        {
                            skipped.Add(label + " — could not build the dimension line");
                            continue;
                        }

                        bool succeeded = false;
                        string lastError = null;
                        foreach (Reference elementReference in CandidateReferences(instance))
                        {
                            var references = new ReferenceArray();
                            references.Append(elementReference);
                            references.Append(targetReference);
                            try
                            {
                                Dimension dimension = doc.Create.NewDimension(view, dimensionLine, references);
                                created.Add(dimension.Id);
                                succeeded = true;
                                break;
                            }
                            catch (Exception ex)
                            {
                                lastError = ex.Message;
                            }
                        }

                        if (!succeeded)
                        {
                            skipped.Add(label + string.Format(" — Revit rejected the dimension ({0})",
                                lastError ?? "no suitable reference"));
                        }
                        continue;
                    }

                    // ---- Linear pipe path ---------------------------------------------
                    MEPCurve pipe = element as MEPCurve;
                    LocationCurve route = element.Location as LocationCurve;
                    if (pipe != null && route != null && route.Curve != null)
                    {
                        XYZ mid = route.Curve.Evaluate(0.5, true);

                        Reference targetReference;
                        Curve targetCurve;
                        string targetDescription;
                        string failure;
                        if (!FindNearestTarget(grids, walls, mid, searchRadiusFeet, preferWalls,
                            out targetReference, out targetCurve, out targetDescription, out failure))
                        {
                            skipped.Add(label + " — " + failure);
                            continue;
                        }

                        IntersectionResult projection = targetCurve.Project(mid);
                        if (projection == null)
                        {
                            skipped.Add(label + " — cannot project the pipe midpoint onto " + targetDescription);
                            continue;
                        }
                        XYZ closestPoint = projection.XYZPoint;

                        XYZ direction = new XYZ(closestPoint.X - mid.X, closestPoint.Y - mid.Y, 0);
                        if (direction.GetLength() < skipFeet)
                        {
                            skipped.Add(label + string.Format(" — pipe midpoint sits within {0} mm of {1}; clearance dimension skipped",
                                skipText, targetDescription));
                            continue;
                        }
                        direction = direction.Normalize();

                        XYZ start = mid.Add(direction.Multiply(StubbleFeet));
                        XYZ end = closestPoint.Add(direction.Multiply(StubbleFeet));
                        Line dimensionLine;
                        try
                        {
                            dimensionLine = Line.CreateBound(start, end);
                        }
                        catch (Exception)
                        {
                            skipped.Add(label + " — could not build the dimension line for the pipe");
                            continue;
                        }

                        bool succeeded = false;
                        string lastError = null;
                        foreach (Reference elementReference in PipeCandidateReferences(pipe, route))
                        {
                            var references = new ReferenceArray();
                            references.Append(elementReference);
                            references.Append(targetReference);
                            try
                            {
                                Dimension dimension = doc.Create.NewDimension(view, dimensionLine, references);
                                created.Add(dimension.Id);
                                succeeded = true;
                                break;
                            }
                            catch (Exception ex)
                            {
                                lastError = ex.Message;
                            }
                        }

                        if (!succeeded)
                        {
                            skipped.Add(label + string.Format(" — Revit rejected the pipe clearance dimension ({0})",
                                lastError ?? "no suitable reference"));
                        }
                        continue;
                    }

                    skipped.Add(label + " — element has no dimensionable location (point family or linear pipe expected)");
                }

                transaction.Commit();
            }

            var resultDialog = new TaskDialog(App.Brand)
            {
                MainInstruction = string.Format("Created {0} clearance dimension{1}.",
                    created.Count, created.Count == 1 ? string.Empty : "s"),
                Content = skipped.Count > 0
                    ? "Skipped elements:\n" + string.Join("\n", skipped.ToArray())
                    : string.Format("Targets: nearest grid centre line or wall face within {0} m{1}.\nView: {2}",
                        settings.ClearanceSearchRadiusMeters.ToString("0.0#"),
                        preferWalls ? ", walls preferred" : "",
                        view.Name)
            };
            resultDialog.CommonButtons = TaskDialogCommonButtons.Ok;
            if (created.Count > 0)
            {
                uidoc.Selection.SetElementIds(created);
                try
                {
                    uidoc.ShowElements(created);
                }
                catch
                {
                    // Zoom-to-element is best-effort.
                }
            }
            resultDialog.Show();
            return Result.Succeeded;
        }

        // ------------------------------------------------------------------
        //  Reference candidates
        // ------------------------------------------------------------------

        private static IEnumerable<Reference> CandidateReferences(FamilyInstance instance)
        {
            FamilyInstanceReferenceType[] preferenceOrder =
            {
                FamilyInstanceReferenceType.CenterLeftRight,
                FamilyInstanceReferenceType.CenterFrontBack,
                FamilyInstanceReferenceType.StrongReference,
                FamilyInstanceReferenceType.WeakReference
            };

            foreach (FamilyInstanceReferenceType referenceType in preferenceOrder)
            {
                Reference[] references;
                try
                {
                    references = instance.GetReferences(referenceType);
                }
                catch
                {
                    continue;
                }
                if (references == null) continue;
                foreach (Reference reference in references) yield return reference;
            }
        }

        /// <summary>
        /// Candidate references for a linear pipe: the stable location-curve reference
        /// first, then a reference built from the element itself (its centreline). Both
        /// are attempted because families and pipe categories differ in what they expose.
        /// </summary>
        private static Reference[] PipeCandidateReferences(MEPCurve pipe, LocationCurve location)
        {
            var candidates = new List<Reference>();
            try
            {
                Reference locationReference = location.Reference;
                if (locationReference != null) candidates.Add(locationReference);
            }
            catch
            {
                // LocationCurve.Reference can be unavailable for some curve types.
            }
            try
            {
                candidates.Add(new Reference(pipe));
            }
            catch
            {
                // Best-effort only.
            }
            return candidates.ToArray();
        }

        // ------------------------------------------------------------------
        //  Nearest-target search
        // ------------------------------------------------------------------

        /// <summary>
        /// Finds the closest grid line or wall to the given point within the configured
        /// search radius. With <paramref name="preferWalls"/> the closest wall wins over
        /// any grid; without it the genuinely closest target of either kind wins.
        /// For walls the side-face reference (interior preferred, exterior fallback) is
        /// returned so the dimension lands on the face, not the wall centreline.
        /// </summary>
        private static bool FindNearestTarget(List<Grid> grids, List<Wall> walls, XYZ point,
            double maxDistanceFeet, bool preferWalls,
            out Reference targetReference, out Curve targetCurve, out string targetDescription, out string failure)
        {
            targetReference = null;
            targetCurve = null;
            targetDescription = null;
            failure = null;

            Reference bestGridReference = null;
            Curve bestGridCurve = null;
            string bestGridDescription = null;
            double bestGridDistance = double.MaxValue;

            Reference bestWallReference = null;
            Curve bestWallCurve = null;
            string bestWallDescription = null;
            double bestWallDistance = double.MaxValue;

            foreach (Grid grid in grids)
            {
                Curve curve = grid.Curve;
                if (curve == null) continue;
                Reference reference = curve.Reference;
                if (reference == null) continue;

                double distance = curve.DistanceTo(point);
                if (distance >= bestGridDistance) continue;
                bestGridDistance = distance;
                bestGridReference = reference;
                bestGridCurve = curve;
                bestGridDescription = "grid '" + grid.Name + "'";
            }

            foreach (Wall wall in walls)
            {
                var locationCurve = wall.Location as LocationCurve;
                if (locationCurve == null || locationCurve.Curve == null) continue;
                Curve curve = locationCurve.Curve;

                double distance = curve.DistanceTo(point);
                if (distance >= bestWallDistance) continue;

                IList<Reference> faces = null;
                try
                {
                    faces = HostObjectUtils.GetSideFaces(wall, ShellLayerType.Interior);
                    if (faces == null || faces.Count == 0)
                    {
                        faces = HostObjectUtils.GetSideFaces(wall, ShellLayerType.Exterior);
                    }
                }
                catch
                {
                    faces = null;
                }
                if (faces == null || faces.Count == 0) continue;

                bestWallDistance = distance;
                bestWallReference = faces[0];
                bestWallCurve = curve;
                bestWallDescription = "wall '" + wall.Name + "' (face)";
            }

            bool useWall;
            if (preferWalls && bestWallReference != null)
            {
                useWall = true;
            }
            else if (bestWallReference != null && bestGridReference != null)
            {
                useWall = bestWallDistance < bestGridDistance;
            }
            else
            {
                useWall = bestWallReference != null;
            }

            if (useWall)
            {
                targetReference = bestWallReference;
                targetCurve = bestWallCurve;
                targetDescription = bestWallDescription;
            }
            else if (bestGridReference != null)
            {
                targetReference = bestGridReference;
                targetCurve = bestGridCurve;
                targetDescription = bestGridDescription;
            }
            else
            {
                failure = "no grid or wall found in the view";
                return false;
            }

            // Enforce the configured search radius (0 disables the limit defensively).
            if (maxDistanceFeet > 0)
            {
                double chosen = useWall ? bestWallDistance : bestGridDistance;
                if (chosen > maxDistanceFeet)
                {
                    failure = string.Format("nearest target ({0}) is beyond the configured search radius",
                        targetDescription);
                    return false;
                }
            }

            return true;
        }
    }
}
