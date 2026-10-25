using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace FFDimAddin2020
{
    internal static class CenterlineJoints
    {
        public static List<XYZ> Compute(List<PipeInfo> pipes, double tolFt)
        {
            List<XYZ> joints = new List<XYZ>();
            for (int i = 0; i < pipes.Count; i++)
                for (int j = i + 1; j < pipes.Count; j++)
                {
                    XYZ J;
                    if (TryJoint(pipes[i], pipes[j], tolFt, out J))
                    {
                        bool dup = false;
                        foreach (XYZ x in joints) if ((x - J).GetLength() < tolFt) { dup = true; break; }
                        if (!dup) joints.Add(J);
                    }
                }
            return joints;
        }

        private static bool TryJoint(PipeInfo a, PipeInfo b, double tol, out XYZ J)
        {
            J = null;
            XYZ da = a.Dir, db = b.Dir;
            double cross = da.X * db.Y - da.Y * db.X;

            if (Math.Abs(cross) < 1e-6)
            {
                XYZ w = b.P0 - a.P0;
                XYZ perp = w - da * w.DotProduct(da);
                if (perp.GetLength() > tol) return false;
                double t0 = w.DotProduct(da);
                double t1 = (w + db * b.Length).DotProduct(da);
                double lo = Math.Max(0.0, Math.Min(t0, t1));
                double hi = Math.Min(a.Length, Math.Max(t0, t1));
                if (lo <= hi) return false;
                if (lo - hi > tol) return false;
                XYZ p = a.P0 + da * ((lo + hi) * 0.5);
                J = new XYZ(p.X, p.Y, a.P0.Z);
                return true;
            }

            XYZ wp = new XYZ(b.P0.X - a.P0.X, b.P0.Y - a.P0.Y, 0);
            double t = (wp.X * db.Y - wp.Y * db.X) / cross;
            XYZ ja = new XYZ(a.P0.X + da.X * t, a.P0.Y + da.Y * t, 0);
            if (DistToSegment(ja, a) > tol) return false;
            if (DistToSegment(ja, b) > tol) return false;
            J = new XYZ(ja.X, ja.Y, a.P0.Z);
            return true;
        }

        private static double DistToSegment(XYZ p, PipeInfo s)
        {
            XYZ a = new XYZ(s.P0.X, s.P0.Y, 0), b = new XYZ(s.P1.X, s.P1.Y, 0);
            XYZ ab = b - a;
            double len2 = ab.X * ab.X + ab.Y * ab.Y;
            if (len2 < 1e-12) return (p - a).GetLength();
            double u = ((p.X - a.X) * ab.X + (p.Y - a.Y) * ab.Y) / len2;
            u = Math.Max(0, Math.Min(1, u));
            XYZ q = a + ab * u;
            return new XYZ(p.X - q.X, p.Y - q.Y, 0).GetLength();
        }
    }

    internal class TickCache
    {
        private const string WitnessStyleName = "Fire Dim Witness";

        private Dictionary<string, Reference> _refs = new Dictionary<string, Reference>();
        private List<KeyValuePair<string, DetailCurve>> _pending = new List<KeyValuePair<string, DetailCurve>>();

        public static TickCache Build(Document doc, View view, List<XYZ> points)
        {
            TickCache c = new TickCache();
            double half = HalfLen(doc);
            GraphicsStyle witness = GetOrCreateWitnessStyle(doc);

            foreach (XYZ p in points)
            {
                c.Create(doc, view, p, XYZ.BasisX, half, witness);
                c.Create(doc, view, p, XYZ.BasisY, half, witness);
            }

            doc.Regenerate();

            foreach (KeyValuePair<string, DetailCurve> kv in c._pending)
            {
                Reference r = kv.Value.GeometryCurve.Reference;
                if (r != null) c._refs[kv.Key] = r;
            }

            if (FireDimSettings.HideTicks && witness != null)
            {
                try { view.SetCategoryHidden(witness.Id, true); }
                catch (Exception) { }
            }
            return c;
        }

        /// On-demand tick (used for wall-face fallback).
        public Reference Ensure(Document doc, View view, XYZ pt, XYZ dir)
        {
            Reference existing = Get(pt, dir);
            if (existing != null) return existing;
            try
            {
                XYZ dd = dir.Normalize();
                double half = HalfLen(doc);
                Line l = Line.CreateBound(pt - dd * half, pt + dd * half);
                DetailCurve dc = doc.Create.NewDetailCurve(view, l);
                doc.Regenerate();
                Reference r = dc.GeometryCurve.Reference;
                if (r != null) _refs[Key(pt, dir)] = r;
                return r;
            }
            catch { return null; }
        }

        public Reference Get(XYZ pt, XYZ dir)
        {
            Reference r;
            _refs.TryGetValue(Key(pt, dir), out r);
            return r;
        }

        private static double HalfLen(Document doc)
        {
            double requested = UnitHelper.MmToFt(FireDimSettings.TickLengthMm);
            double minLen = doc.Application.ShortCurveTolerance * 1.05;
            return Math.Max(requested, minLen) * 0.5;
        }

        private void Create(Document doc, View view, XYZ pt, XYZ dir, double half, GraphicsStyle style)
        {
            string key = Key(pt, dir);
            foreach (KeyValuePair<string, DetailCurve> kv in _pending) if (kv.Key == key) return;
            try
            {
                XYZ dd = dir.Normalize();
                Line l = Line.CreateBound(pt - dd * half, pt + dd * half);
                DetailCurve dc = doc.Create.NewDetailCurve(view, l);
                if (style != null) { try { dc.LineStyle = style; } catch (Exception) { } }
                _pending.Add(new KeyValuePair<string, DetailCurve>(key, dc));
            }
            catch (Exception) { }
        }

        private static GraphicsStyle GetOrCreateWitnessStyle(Document doc)
        {
            try
            {
                Category parent = null;
                try { parent = doc.Settings.Categories.get_Item("Detail Lines"); }
                catch (Exception) { parent = null; }
                if (parent == null) return null;

                foreach (Category sub in parent.SubCategories)
                {
                    if (sub.Name == WitnessStyleName)
                        return sub.GetGraphicsStyle(GraphicsStyleType.Projection);
                }

                Category created = doc.Settings.Categories.NewSubcategory(parent, WitnessStyleName);
                return created != null ? created.GetGraphicsStyle(GraphicsStyleType.Projection) : null;
            }
            catch (Exception) { return null; }
        }

        private static string Key(XYZ pt, XYZ dir)
        {
            XYZ d = dir.Normalize();
            if (d.X < -1e-9 || (Math.Abs(d.X) < 1e-9 && d.Y < -1e-9)) d = -d;
            return ((int)Math.Round(pt.X * 304.8)) + "_" + ((int)Math.Round(pt.Y * 304.8)) + "_"
                 + ((int)Math.Round(d.X * 1000)) + "_" + ((int)Math.Round(d.Y * 1000));
        }
    }
}