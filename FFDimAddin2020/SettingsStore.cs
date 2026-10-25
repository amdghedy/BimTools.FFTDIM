using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;

namespace FFDimAddin2020
{
    internal static class SettingsStore
    {
        private static string FilePath
        {
            get { return Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "FireDimSettings.txt"); }
        }

        public static void Load()
        {
            try
            {
                if (!File.Exists(FilePath)) return;
                foreach (string line in File.ReadAllLines(FilePath))
                {
                    int i = line.IndexOf('=');
                    if (i <= 0) continue;
                    Apply(line.Substring(0, i).Trim(), line.Substring(i + 1).Trim());
                }
            }
            catch { }
        }

        public static void Save()
        {
            try
            {
                List<string> l = new List<string>();
                l.Add("FilterMode=" + (int)FireDimSettings.FilterMode);
                l.Add("IncludeAnyPipingSystem=" + (FireDimSettings.IncludeAnyPipingSystem ? "1" : "0"));
                l.Add("Keywords=" + string.Join(",", FireDimSettings.FireKeywords));
                l.Add("CreateRunDimensions=" + (FireDimSettings.CreateRunDimensions ? "1" : "0"));
                l.Add("CreateWallDimensions=" + (FireDimSettings.CreateWallDimensions ? "1" : "0"));
                l.Add("HideTicks=" + (FireDimSettings.HideTicks ? "1" : "0"));
                l.Add("DimOffsetMm=" + FireDimSettings.DimOffsetMm.ToString(CultureInfo.InvariantCulture));
                l.Add("MaxWallSearchMm=" + FireDimSettings.MaxWallSearchMm.ToString(CultureInfo.InvariantCulture));
                l.Add("MinSegmentMm=" + FireDimSettings.MinSegmentMm.ToString(CultureInfo.InvariantCulture));
                l.Add("MaxFittingGapMm=" + FireDimSettings.MaxFittingGapMm.ToString(CultureInfo.InvariantCulture));
                l.Add("DimensionSide=" + FireDimSettings.DimensionSide);
                File.WriteAllLines(FilePath, l);
            }
            catch { }
        }

        private static void Apply(string k, string v)
        {
            try
            {
                switch (k)
                {
                    case "FilterMode": FireDimSettings.FilterMode = (FireDimSettings.FireFilterMode)int.Parse(v); break;
                    case "IncludeAnyPipingSystem": FireDimSettings.IncludeAnyPipingSystem = v == "1"; break;
                    case "Keywords":
                        List<string> kw = new List<string>();
                        foreach (string s in v.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)) kw.Add(s.Trim());
                        FireDimSettings.FireKeywords = kw.ToArray();
                        break;
                    case "CreateRunDimensions": FireDimSettings.CreateRunDimensions = v == "1"; break;
                    case "CreateWallDimensions": FireDimSettings.CreateWallDimensions = v == "1"; break;
                    case "HideTicks": FireDimSettings.HideTicks = v == "1"; break;
                    case "DimOffsetMm": FireDimSettings.DimOffsetMm = double.Parse(v, CultureInfo.InvariantCulture); break;
                    case "MaxWallSearchMm": FireDimSettings.MaxWallSearchMm = double.Parse(v, CultureInfo.InvariantCulture); break;
                    case "MinSegmentMm": FireDimSettings.MinSegmentMm = double.Parse(v, CultureInfo.InvariantCulture); break;
                    case "MaxFittingGapMm": FireDimSettings.MaxFittingGapMm = double.Parse(v, CultureInfo.InvariantCulture); break;
                    case "DimensionSide": FireDimSettings.DimensionSide = int.Parse(v); break;
                }
            }
            catch { }
        }
    }
}