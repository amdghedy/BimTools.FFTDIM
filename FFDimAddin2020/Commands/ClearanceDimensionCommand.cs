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
    /// For each selected sprinkler / point fitting in a plan view, finds the nearest grid line
    /// or wall in that view and creates a clearance dimension from the element's centre
    /// reference to the grid line (centre) or wall face (interior face preferred, exterior as
    /// fallback). Elements closer than 50 mm to the target are skipped — a near-zero dimension
    /// has no value. All failures are reported per element; the transaction commits whatever
    /// was created successfully.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class ClearanceDimensionCommand : CommandBase
    {
        private const double StubbleFeet = 0.15;           // dim line stub away from reference planes
        private const double MinDistanceFeet = 0.164042;   // 50 mm — below this a clearance dim is meaningless

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
                    "Select one or more sprinklers or point fittings in a plan view, then run this command " +
                    "to dimension each element to its nearest grid line or wall.");
                return Result.Cancelled;
            }

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

                    FamilyInstance instance = element as FamilyInstance;
                    LocationPoint location = element.Location as LocationPoint;
                    if (instance == null || location == null)
                    {
                        skipped.Add(label + " — only point-based families are supported (pipes are not yet dimensioned here)");
                        continue;
                    }

                    XYZ point = location.Point;

                    Reference targetReference;
                    Curve targetCurve;
                    string targetDescription;
                    string failure;
                    if (!FindNearestTarget(grids, walls, point, out targetReference, out targetCurve, out targetDescription, out failure))
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
                    if (direction.GetLength() < MinDistanceFeet)
                    {
                        skipped.Add(label + string.Format(" — sits within 50 mm of {0}; clearance dimension skipped", targetDescription));
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
                }

                transaction.Commit();
            }

            var resultDialog = new TaskDialog(App.Brand)
            {
                MainInstruction = string.Format("Created {0} clearance dimension{1}.",
                    created.Count, created.Count == 1 ? string.Empty : "s"),
                Content = skipped.Count > 0
                    ? "Skipped elements:\n" + string.Join("\n", skipped.ToArray())
                    : string.Format("Targets: nearest grid centre line or wall interior face, whichever is closer.\nView: {0}", view.Name)
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

        private static bool FindNearestTarget(List<Grid> grids, List<Wall> walls, XYZ point,
            out Reference targetReference, out Curve targetCurve, out string targetDescription, out string failure)
        {
            targetReference = null;
            targetCurve = null;
            targetDescription = null;
            failure = null;

            double bestDistance = double.MaxValue;

            foreach (Grid grid in grids)
            {
                Curve curve = grid.Curve;
                if (curve == null) continue;
                Reference reference = curve.Reference;
                if (reference == null) continue;

                double distance = curve.DistanceTo(point);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    targetReference = reference;
                    targetCurve = curve;
                    targetDescription = "grid '" + grid.Name + "'";
                }
            }

            foreach (Wall wall in walls)
            {
                var locationCurve = wall.Location as LocationCurve;
                if (locationCurve == null || locationCurve.Curve == null) continue;
                Curve curve = locationCurve.Curve;

                double distance = curve.DistanceTo(point);
                if (distance < bestDistance)
                {
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

                    bestDistance = distance;
                    targetReference = faces[0];
                    targetCurve = curve;
                    targetDescription = "wall '" + wall.Name + "' (face)";
                }
            }

            if (targetReference == null)
            {
                failure = "no grid or wall found in the view";
                return false;
            }
            return true;
        }
    }
}
