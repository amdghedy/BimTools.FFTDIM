using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace FFDimAddin2020
{
    internal static class PipeRunGrouper
    {
        public static List<List<PipeInfo>> Group(List<PipeInfo> pipes)
        {
            List<List<PipeInfo>> runs = new List<List<PipeInfo>>();
            foreach (PipeInfo pi in pipes)
            {
                List<PipeInfo> target = null;
                foreach (List<PipeInfo> run in runs)
                {
                    if (IsCollinear(run[0], pi)) { target = run; break; }
                }
                if (target == null) { target = new List<PipeInfo>(); runs.Add(target); }
                target.Add(pi);
            }
            return runs;
        }

        private static bool IsCollinear(PipeInfo a, PipeInfo b)
        {
            if (Math.Abs(a.Dir.DotProduct(b.Dir)) < 0.999) return false;

            XYZ w = b.P0 - a.P0;
            XYZ perp = w - a.Dir * w.DotProduct(a.Dir);
            if (perp.GetLength() > UnitHelper.MmToFt(FireDimSettings.CollinearTolMm)) return false;

            double t0b = w.DotProduct(a.Dir);
            double t1b = (w + b.Dir * b.Length).DotProduct(a.Dir);
            double lo = Math.Max(0, Math.Min(t0b, t1b));
            double hi = Math.Min(a.Length, Math.Max(t0b, t1b));
            double gap = lo > hi ? lo - hi : 0;
            return gap <= UnitHelper.MmToFt(FireDimSettings.MaxFittingGapMm);
        }
    }
}