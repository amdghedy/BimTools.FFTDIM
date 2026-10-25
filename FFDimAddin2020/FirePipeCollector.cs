using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Plumbing;

namespace FFDimAddin2020
{
    internal class EndFace
    {
        public Reference Ref;
        public XYZ Point;
        public XYZ Normal;
        public XYZ BBoxMin;
        public XYZ BBoxMax;
    }

    internal class PipeInfo
    {
        public Pipe Pipe;
        public XYZ P0, P1, Dir;
        public double Length;
        public XYZ Mid { get { return (P0 + P1) * 0.5; } }
    }

    internal static class FirePipeCollector
    {
        public static List<PipeInfo> Collect(Document doc, View view)
        {
            List<PipeInfo> result = new List<PipeInfo>();
            FilteredElementCollector col = new FilteredElementCollector(doc, view.Id).OfClass(typeof(Pipe));

            foreach (Pipe pipe in col)
            {
                if (!IsFirePipe(doc, pipe)) continue;

                LocationCurve lc = pipe.Location as LocationCurve;
                Line axis = lc != null ? lc.Curve as Line : null;
                if (axis == null) continue;

                PipeInfo pi = new PipeInfo();
                pi.Pipe = pipe;
                pi.P0 = axis.GetEndPoint(0);
                pi.P1 = axis.GetEndPoint(1);
                XYZ d = new XYZ(axis.Direction.X, axis.Direction.Y, 0);
                if (d.GetLength() < 1e-9) continue; // vertical pipe: no plan length
                pi.Dir = d.Normalize();
                pi.Length = (pi.P1 - pi.P0).GetLength();
                result.Add(pi);
            }
            return result;
        }

        private static bool IsFirePipe(Document doc, Pipe pipe)
        {
            if (FireDimSettings.FilterMode == FireDimSettings.FireFilterMode.AllPipesInView)
                return true;

            PipingSystemType pst = GetSystemType(doc, pipe);
            if (pst != null)
            {
                MEPSystemClassification sc = pst.SystemClassification;
                if (sc == MEPSystemClassification.FireProtectWet ||
                    sc == MEPSystemClassification.FireProtectDry ||
                    sc == MEPSystemClassification.FireProtectPreaction ||
                    sc == MEPSystemClassification.FireProtectOther)
                    return true;

                if (FireDimSettings.IncludeAnyPipingSystem) return true;
                if (ContainsKeyword(pst.Name)) return true;
            }

            try
            {
                MEPSystem sys = pipe.MEPSystem;
                if (sys != null && ContainsKeyword(sys.Name)) return true;
            }
            catch { }

            try
            {
                PipeType pt = doc.GetElement(pipe.GetTypeId()) as PipeType;
                if (pt != null && ContainsKeyword(pt.Name)) return true;
            }
            catch { }

            return false;
        }

        private static PipingSystemType GetSystemType(Document doc, Pipe pipe)
        {
            try
            {
                MEPSystem sys = pipe.MEPSystem;
                if (sys == null) return null;
                return doc.GetElement(sys.GetTypeId()) as PipingSystemType;
            }
            catch { return null; }
        }

        private static bool ContainsKeyword(string s)
        {
            if (string.IsNullOrEmpty(s)) return false;
            string u = s.ToUpperInvariant();
            foreach (string k in FireDimSettings.FireKeywords)
            {
                if (!string.IsNullOrEmpty(k) && u.Contains(k.ToUpperInvariant())) return true;
            }
            return false;
        }
    }
}