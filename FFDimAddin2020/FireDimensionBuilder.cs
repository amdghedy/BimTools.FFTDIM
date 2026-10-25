using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace FFDimAddin2020
{
    internal static class FireDimensionBuilder
    {
        public static int FailedRun = 0, FailedWall = 0;
        public static int SkippedDuplicates = 0;

        private static HashSet<string> _created = new HashSet<string>();   // exact-span dedupe
        private static HashSet<string> _chainSigs = new HashSet<string>(); // station-pattern dedupe
        public static void ResetDedupe() { _created.Clear(); _chainSigs.Clear(); SkippedDuplicates = 0; }

        private class RefEntry
        {
            public double T;
            public Reference Ref;
            public EndFace Wall;
            public XYZ Anchor;
            public XYZ N;
            public XYZ Point;
        }

        private static string Signature(List<RefEntry> entries)
        {
            List<RefEntry> ord = entries.OrderBy(x => x.T).ToList();
            XYZ a = ord[0].Point;
            XYZ b = ord[ord.Count - 1].Point;
            XYZ d = new XYZ(b.X - a.X, b.Y - a.Y, 0);
            if (d.GetLength() < 1e-9) d = XYZ.BasisX; else d = d.Normalize();
            if (d.X < -1e-9 || (Math.Abs(d.X) < 1e-9 && d.Y < -1e-9)) d = -d;

            return ((int)Math.Round(d.X * 1000)) + "_" + ((int)Math.Round(d.Y * 1000)) + "_" +
                   ((int)Math.Round(UnitHelper.FtToMm(a.X) / 50.0)) + "_" + ((int)Math.Round(UnitHelper.FtToMm(a.Y) / 50.0)) + "_" +
                   ((int)Math.Round(UnitHelper.FtToMm(b.X) / 50.0)) + "_" + ((int)Math.Round(UnitHelper.FtToMm(b.Y) / 50.0));
        }

        /// Direction + joint stations along the route (perpendicular position ignored).
        private static string ChainSignature(XYZ d, XYZ origin, List<XYZ> kept)
        {
            string s = ((int)Math.Round(d.X * 1000)) + "_" + ((int)Math.Round(d.Y * 1000));
            foreach (XYZ J in kept)
                s += "_" + ((int)Math.Round(UnitHelper.FtToMm((J - origin).DotProduct(d)) / 50.0));
            return s;
        }

        private static Dimension TryCreate(Document doc, View view, Line line,
            List<RefEntry> entries, TickCache ticks, bool wallsAsTicks, bool dropWalls)
        {
            ReferenceArray ra = new ReferenceArray();
            foreach (RefEntry e in entries.OrderBy(x => x.T))
            {
                Reference r = e.Ref;
                if (e.Wall != null)
                {
                    if (dropWalls) continue;
                    if (wallsAsTicks)
                    {
                        double dist = (e.Anchor - e.Wall.Point).DotProduct(e.N);
                        XYZ q = e.Anchor - e.N * dist;
                        r = ticks.Ensure(doc, view, q, e.N.CrossProduct(XYZ.BasisZ));
                    }
                }
                if (r == null) return null;
                ra.Append(r);
            }
            if (ra.Size < 2) return null;
            try { return doc.Create.NewDimension(view, line, ra); }
            catch (Exception) { return null; }
        }

        private static int CreateWithFallback(Document doc, View view, Line line,
            List<RefEntry> entries, TickCache ticks)
        {
            string sig = Signature(entries);
            if (_created.Contains(sig)) { SkippedDuplicates++; return 2; }

            Dimension dim = TryCreate(doc, view, line, entries, ticks, false, false);
            if (dim == null) dim = TryCreate(doc, view, line, entries, ticks, true, false);
            if (dim == null) dim = TryCreate(doc, view, line, entries, ticks, true, true);

            if (dim != null) { _created.Add(sig); return 1; }
            return 0;
        }

        private static XYZ WallQ(XYZ anchor, EndFace wall, XYZ n)
        {
            double dist = (anchor - wall.Point).DotProduct(n);
            return anchor - n * dist;
        }

        /// Chain ALONG the run: [wall] -> fitting centers -> [wall].
        /// Parallel routes with identical joint stations are dimensioned ONCE.
        public static int BuildRunDimensions(Document doc, View view,
            List<List<PipeInfo>> runs, List<XYZ> joints, List<EndFace> walls, TickCache ticks)
        {
            int count = 0;
            double tol = UnitHelper.MmToFt(FireDimSettings.CollinearTolMm);
            double gapTol = UnitHelper.MmToFt(FireDimSettings.MaxFittingGapMm);
            double maxWall = UnitHelper.MmToFt(FireDimSettings.MaxWallSearchMm);

            foreach (List<PipeInfo> run in runs)
            {
                try
                {
                    XYZ d = run[0].Dir;
                    XYZ tickDir = d.CrossProduct(XYZ.BasisZ).Normalize();
                    XYZ side = tickDir * UnitHelper.MmToFt(FireDimSettings.DimOffsetMm) * FireDimSettings.DimensionSide;
                    XYZ origin = run[0].P0;

                    double minT = double.MaxValue, maxT = double.MinValue;
                    foreach (PipeInfo pi in run)
                        foreach (XYZ e in new List<XYZ> { pi.P0, pi.P1 })
                        {
                            double t = (e - origin).DotProduct(d);
                            minT = Math.Min(minT, t); maxT = Math.Max(maxT, t);
                        }

                    List<XYZ> onAxis = new List<XYZ>();
                    foreach (XYZ J in joints)
                    {
                        XYZ w = J - origin;
                        double t = w.DotProduct(d);
                        if ((w - d * t).GetLength() > tol) continue;
                        if (t < minT - gapTol || t > maxT + gapTol) continue;
                        onAxis.Add(J);
                    }
                    onAxis = onAxis.OrderBy(p => (p - origin).DotProduct(d)).ToList();

                    List<XYZ> kept = new List<XYZ>();
                    double lastT = double.MinValue;
                    foreach (XYZ J in onAxis)
                    {
                        double t = (J - origin).DotProduct(d);
                        if (t - lastT < UnitHelper.MmToFt(FireDimSettings.MinSegmentMm)) continue;
                        kept.Add(J); lastT = t;
                    }
                    if (kept.Count < 1) continue;

                    // NEW: skip parallel routes whose joint stations were already dimensioned
                    string chainSig = ChainSignature(d, origin, kept);
                    if (_chainSigs.Contains(chainSig)) { SkippedDuplicates++; continue; }
                    _chainSigs.Add(chainSig);

                    List<RefEntry> entries = new List<RefEntry>();

                    EndFace wStart = WallFaceIndex.FindNearest(walls, kept[0], d, maxWall);
                    if (wStart != null)
                    {
                        XYZ q = WallQ(kept[0], wStart, d);
                        entries.Add(new RefEntry { T = (q - origin).DotProduct(d), Wall = wStart, Anchor = kept[0], N = d, Point = q });
                    }

                    bool ok = true;
                    foreach (XYZ J in kept)
                    {
                        Reference r = ticks.Get(J, tickDir);
                        if (r == null) { ok = false; break; }
                        entries.Add(new RefEntry { T = (J - origin).DotProduct(d), Ref = r, Point = J });
                    }
                    if (!ok) continue;

                    EndFace wEnd = WallFaceIndex.FindNearest(walls, kept[kept.Count - 1], -d, maxWall);
                    if (wEnd != null)
                    {
                        XYZ q = WallQ(kept[kept.Count - 1], wEnd, -d);
                        entries.Add(new RefEntry { T = (q - origin).DotProduct(d), Wall = wEnd, Anchor = kept[kept.Count - 1], N = -d, Point = q });
                    }

                    if (entries.Count < 2) continue;

                    double t0 = entries.Min(x => x.T), t1 = entries.Max(x => x.T);
                    XYZ basePt = origin + side;
                    Line line = Line.CreateBound(basePt + d * (t0 - 1.0), basePt + d * (t1 + 1.0));

                    int res = CreateWithFallback(doc, view, line, entries, ticks);
                    if (res == 1) count++; else if (res == 0) FailedRun++;
                }
                catch (Exception) { FailedRun++; }
            }
            return count;
        }

        /// ONE perpendicular dimension to the nearest parallel wall PER straight route.
        public static int BuildWallDimensions(Document doc, View view,
            List<List<PipeInfo>> runs, List<EndFace> walls, List<XYZ> joints, TickCache ticks)
        {
            int count = 0;
            double maxDist = UnitHelper.MmToFt(FireDimSettings.MaxWallSearchMm);
            double tol = UnitHelper.MmToFt(FireDimSettings.CollinearTolMm);
            double gapTol = UnitHelper.MmToFt(FireDimSettings.MaxFittingGapMm);

            foreach (List<PipeInfo> run in runs)
            {
                try
                {
                    XYZ d = run[0].Dir;
                    XYZ n = d.CrossProduct(XYZ.BasisZ).Normalize();
                    XYZ tickDir = n.CrossProduct(XYZ.BasisZ).Normalize();
                    XYZ origin = run[0].P0;

                    double minT = double.MaxValue, maxT = double.MinValue;
                    foreach (PipeInfo pi in run)
                        foreach (XYZ e in new List<XYZ> { pi.P0, pi.P1 })
                        {
                            double t = (e - origin).DotProduct(d);
                            minT = Math.Min(minT, t); maxT = Math.Max(maxT, t);
                        }

                    XYZ anchor = run[0].P0;
                    double bestT = double.MaxValue;
                    foreach (XYZ J in joints)
                    {
                        XYZ w = J - origin;
                        double t = w.DotProduct(d);
                        if ((w - d * t).GetLength() > tol) continue;
                        if (t < minT - gapTol || t > maxT + gapTol) continue;
                        if (t < bestT) { bestT = t; anchor = J; }
                    }

                    Reference tickRef = ticks.Get(anchor, tickDir);
                    if (tickRef == null) tickRef = ticks.Ensure(doc, view, anchor, tickDir);
                    if (tickRef == null) continue;

                    XYZ dir = n;
                    EndFace wf = WallFaceIndex.FindNearest(walls, anchor, dir, maxDist);
                    if (wf == null) { dir = -n; wf = WallFaceIndex.FindNearest(walls, anchor, dir, maxDist); }
                    if (wf == null) continue;

                    XYZ q = WallQ(anchor, wf, dir);
                    XYZ side = d * UnitHelper.MmToFt(FireDimSettings.DimOffsetMm) * FireDimSettings.DimensionSide;
                    Line line = Line.CreateBound(q + side - dir * 1.0, anchor + side + dir * 1.0);

                    List<RefEntry> entries = new List<RefEntry>
                    {
                        new RefEntry { T = 0, Wall = wf, Anchor = anchor, N = dir, Point = q },
                        new RefEntry { T = (anchor - q).DotProduct(dir), Ref = tickRef, Point = anchor }
                    };

                    int res = CreateWithFallback(doc, view, line, entries, ticks);
                    if (res == 1) count++; else if (res == 0) FailedWall++;
                }
                catch (Exception) { FailedWall++; }
            }
            return count;
        }
    }
}