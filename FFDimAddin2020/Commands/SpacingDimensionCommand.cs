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
    /// FFD-05 · Spacing Chain (model-changing).
    /// Select two or more point-based fire-fighting families (sprinklers, point fittings) in a
    /// plan view. FFTDIM orders their insertion points along the dominant axis of the view and
    /// creates ONE multi-segment dimension through their centre reference planes, offset
    /// perpendicular to the chain (default 1 m — configurable in Settings & Options) so it
    /// never sits on top of the elements.
    ///
    /// Families that expose no usable centre reference plane are skipped and reported.
    /// A single transaction wraps the creation; on failure it is rolled back.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class SpacingDimensionCommand : CommandBase
    {
        private const double LineExtensionFeetFallback = 0.3; // used only if settings are unreadable

        private class SpacingItem
        {
            public Element Element;
            public XYZ Point;
            public Reference Reference;
        }

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
            if (selection.Count < 2)
            {
                TaskDialog.Show(App.Brand,
                    "Select at least two sprinklers or point fittings in a plan view, then run this command " +
                    "to create a spacing chain dimension through their centre references.");
                return Result.Cancelled;
            }

            var items = new List<SpacingItem>();
            var skipped = new List<string>();

            foreach (ElementId id in selection)
            {
                Element element = doc.GetElement(id);
                if (element == null) continue;
                string label = Describe(element);

                FamilyInstance instance = element as FamilyInstance;
                LocationPoint location = element.Location as LocationPoint;
                if (instance == null || location == null)
                {
                    skipped.Add(label + " — not a point-based family");
                    continue;
                }

                Reference reference = GetCenterReference(instance);
                if (reference == null)
                {
                    skipped.Add(label + " — family exposes no centre reference plane");
                    continue;
                }

                items.Add(new SpacingItem { Element = element, Point = location.Point, Reference = reference });
            }

            if (items.Count < 2)
            {
                TaskDialog.Show(App.Brand, "Not enough dimensionable elements in the selection.\n\nSkipped:\n" +
                                           string.Join("\n", skipped.ToArray()));
                return Result.Cancelled;
            }

            AppSettings settings = AppSettingsStore.LoadOrDefaults();
            L.Apply(settings);

            // Dominant axis of the selection inside the current plan view.
            XYZ right = Flatten(view.RightDirection, XYZ.BasisX);
            XYZ up = Flatten(view.UpDirection, XYZ.BasisY);
            bool alongRight = Spread(items.Select(i => i.Point), right) >= Spread(items.Select(i => i.Point), up);
            XYZ axis = alongRight ? right : up;
            XYZ perpendicular = alongRight ? up : right;

            items.Sort((a, b) => Dot(a.Point, axis).CompareTo(Dot(b.Point, axis)));

            double extension = settings.ChainExtensionFeet;
            double offset = settings.ChainOffsetFeet;
            if (extension <= 0) extension = LineExtensionFeetFallback;
            if (offset <= 0) offset = UnitHelper.FeetFromMeters(1.0);
            double startProjection = Dot(items[0].Point, axis);
            double endProjection = Dot(items[items.Count - 1].Point, axis);

            XYZ lineStart = axis.Multiply(startProjection - extension).Add(perpendicular.Multiply(offset));
            XYZ lineEnd = axis.Multiply(endProjection + extension).Add(perpendicular.Multiply(offset));

            Line dimensionLine;
            try
            {
                dimensionLine = Line.CreateBound(lineStart, lineEnd);
            }
            catch (Exception)
            {
                TaskDialog.Show(App.Brand, "Could not build a dimension line for this selection " +
                                           "(the points may collapse onto one another).");
                return Result.Cancelled;
            }

            var references = new ReferenceArray();
            foreach (SpacingItem item in items) references.Append(item.Reference);

            Dimension dimension;
            using (Transaction transaction = new Transaction(doc, "FFTDIM: Spacing chain dimension"))
            {
                transaction.Start();
                try
                {
                    dimension = doc.Create.NewDimension(view, dimensionLine, references);
                    transaction.Commit();
                }
                catch (Exception ex)
                {
                    transaction.RollBack();
                    TaskDialog.Show(App.Brand,
                        "Revit rejected the spacing dimension:\n\n" + ex.Message +
                        "\n\nThe selected families may not expose centre reference planes suitable for " +
                        "dimensioning. Try sprinkler families with standard centre references.");
                    return Result.Failed;
                }
            }

            uidoc.Selection.SetElementIds(new List<ElementId> { dimension.Id });
            try
            {
                uidoc.ShowElements(new List<ElementId> { dimension.Id });
            }
            catch
            {
                // Zoom-to-element is best-effort.
            }

            string note = skipped.Count > 0 ? "\n\nSkipped elements:\n" + string.Join("\n", skipped.ToArray()) : string.Empty;
            TaskDialog.Show(App.Brand, string.Format("Spacing dimension created across {0} elements.", items.Count) + note);
            return Result.Succeeded;
        }

        private static string Describe(Element element)
        {
            string category = element.Category != null ? element.Category.Name : "Element";
            return string.Format("{0} '{1}'", category, element.Name);
        }

        /// <summary>Returns the first usable centre reference of the family, or null.</summary>
        private static Reference GetCenterReference(FamilyInstance instance)
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
                try
                {
                    Reference[] references = instance.GetReferences(referenceType);
                    if (references != null && references.Length > 0) return references[0];
                }
                catch
                {
                    // Some families throw for unsupported reference types; keep trying.
                }
            }
            return null;
        }

        private static XYZ Flatten(XYZ direction, XYZ fallback)
        {
            var flat = new XYZ(direction.X, direction.Y, 0);
            if (flat.GetLength() < 1e-9) flat = new XYZ(fallback.X, fallback.Y, 0);
            if (flat.GetLength() < 1e-9) flat = XYZ.BasisX;
            return flat.Normalize();
        }

        private static double Spread(IEnumerable<XYZ> points, XYZ axis)
        {
            double min = double.MaxValue;
            double max = double.MinValue;
            foreach (XYZ point in points)
            {
                double projection = Dot(point, axis);
                if (projection < min) min = projection;
                if (projection > max) max = projection;
            }
            return max - min;
        }

        private static double Dot(XYZ point, XYZ axis)
        {
            return point.X * axis.X + point.Y * axis.Y;
        }
    }
}
