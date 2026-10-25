namespace FFDimAddin2020
{
    public static class FireDimSettings
    {
        public enum FireFilterMode { BySystemClassification, ByNameContains, AllPipesInView }

        public static FireFilterMode FilterMode = FireFilterMode.BySystemClassification;
        public static string[] FireKeywords = { "FIRE", "SPRINK", "HYDRANT", "FF", "PIPE", "HYDRONIC" };
        public static bool IncludeAnyPipingSystem = true;

        public static double DimOffsetMm = 600;   // dimension line offset from pipe centerline
        public static double MaxWallSearchMm = 8000;  // max wall-to-pipe dimension distance
        public static double MinSegmentMm = 100;   // skip joints closer than this
        public static double CollinearTolMm = 25;    // same-run tolerance
        public static double MaxFittingGapMm = 500;   // max gap across a fitting
        public static double TickLengthMm = 1.0;   // witness tick length (clamped to Revit minimum)
        public static bool HideTicks = true;  // hide witness ticks after dimensioning

        public static bool CreateRunDimensions = true; // chains along runs (+wall at both ends)
        public static bool CreateWallDimensions = true; // ONE perpendicular wall dim per straight route

        public static int DimensionSide = 1;  // +1 / -1 flips dimension line side
    }

    internal static class UnitHelper
    {
        public static double MmToFt(double mm)
        { return Autodesk.Revit.DB.UnitUtils.ConvertToInternalUnits(mm, Autodesk.Revit.DB.DisplayUnitType.DUT_MILLIMETERS); }
        public static double FtToMm(double ft)
        { return Autodesk.Revit.DB.UnitUtils.ConvertFromInternalUnits(ft, Autodesk.Revit.DB.DisplayUnitType.DUT_MILLIMETERS); }
    }
}