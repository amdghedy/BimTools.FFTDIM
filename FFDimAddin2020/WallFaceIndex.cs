using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace FFDimAddin2020
{
    internal static class WallFaceIndex
    {
        public static List<EndFace> Build(Document doc, View view)
        {
            List<EndFace> list = new List<EndFace>();
            Options opt = new Options();
            opt.ComputeReferences = true;
            opt.DetailLevel = ViewDetailLevel.Fine;

            // 1) walls in THIS model
            foreach (Wall wall in new FilteredElementCollector(doc, view.Id).OfClass(typeof(Wall)))
            {
                try { AddFaces(wall.get_Geometry(opt), list); } catch { }
            }

            // 2) walls coming from LINKED models
            foreach (RevitLinkInstance link in new FilteredElementCollector(doc, view.Id).OfClass(typeof(RevitLinkInstance)))
            {
                try
                {
                    GeometryElement geo = link.get_Geometry(opt);
                    if (geo == null) continue;
                    foreach (GeometryObject go in geo)
                    {
                        GeometryInstance gi = go as GeometryInstance;
                        if (gi == null) continue;
                        AddFaces(gi.GetInstanceGeometry(), list);
                    }
                }
                catch { }
            }

            // 3) fallback: whole document if view-scoped collector found nothing
            if (list.Count == 0)
            {
                foreach (Wall wall in new FilteredElementCollector(doc).OfClass(typeof(Wall)))
                {
                    try { AddFaces(wall.get_Geometry(opt), list); } catch { }
                }
            }
            return list;
        }

        private static void AddFaces(GeometryElement geo, List<EndFace> list)
        {
            if (geo == null) return;
            foreach (GeometryObject go in geo)
            {
                Solid solid = go as Solid;
                if (solid == null || solid.Faces == null || solid.Faces.Size == 0) continue;
                foreach (Face face in solid.Faces)
                {
                    PlanarFace pf = face as PlanarFace;
                    if (pf == null) continue;
                    XYZ n = pf.FaceNormal;
                    if (n == null || Math.Abs(n.Z) > 0.01) continue; // vertical faces only
                    Reference r = pf.Reference;
                    if (r == null) continue;

                    BoundingBoxUV bbuv = pf.GetBoundingBox();
                    XYZ c1 = pf.Evaluate(new UV(bbuv.Min.U, bbuv.Min.V));
                    XYZ c2 = pf.Evaluate(new UV(bbuv.Max.U, bbuv.Min.V));
                    XYZ c3 = pf.Evaluate(new UV(bbuv.Min.U, bbuv.Max.V));
                    XYZ c4 = pf.Evaluate(new UV(bbuv.Max.U, bbuv.Max.V));

                    list.Add(new EndFace
                    {
                        Ref = r,
                        Point = pf.Origin,
                        Normal = n.Normalize(),
                        BBoxMin = new XYZ(
                            Math.Min(Math.Min(c1.X, c2.X), Math.Min(c3.X, c4.X)),
                            Math.Min(Math.Min(c1.Y, c2.Y), Math.Min(c3.Y, c4.Y)), 0),
                        BBoxMax = new XYZ(
                            Math.Max(Math.Max(c1.X, c2.X), Math.Max(c3.X, c4.X)),
                            Math.Max(Math.Max(c1.Y, c2.Y), Math.Max(c3.Y, c4.Y)), 0)
                    });
                }
            }
        }

        /// Nearest wall face whose plane is perpendicular to n (tolerance ~2 degrees).
        public static EndFace FindNearest(List<EndFace> walls, XYZ point, XYZ n, double maxDistFt)
        {
            EndFace best = null;
            double bestDist = double.MaxValue;
            foreach (EndFace w in walls)
            {
                if (Math.Abs(w.Normal.DotProduct(n)) < 0.98) continue;
                double dist = (point - w.Point).DotProduct(n);
                if (dist <= 0 || dist > maxDistFt) continue;
                if (dist < bestDist) { bestDist = dist; best = w; }
            }
            return best;
        }
    }
}