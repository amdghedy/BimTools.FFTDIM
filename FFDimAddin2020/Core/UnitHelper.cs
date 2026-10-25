using Autodesk.Revit.DB;

namespace BimTools.FFTDIM.Core
{
    /// <summary>
    /// Unit conversion and formatting helpers. Revit's internal unit for length is the foot;
    /// user-facing values are formatted through the project's unit settings.
    /// </summary>
    public static class UnitHelper
    {
        public const double FeetPerMeter = 3.28083989501312;
        public const double MmPerFoot = 304.8;

        /// <summary>Formats an internal (feet) length using the document's project units.</summary>
        public static string FormatLength(Document doc, double feetValue)
        {
            try
            {
                return UnitFormatUtils.Format(doc.GetUnits(), SpecTypeId.Length, feetValue, false);
            }
            catch
            {
                // Fall back to millimetres if formatting is unavailable for any reason.
                return (feetValue * MmPerFoot).ToString("F1") + " mm";
            }
        }

        public static double FeetFromMillimeters(double mm)
        {
            return mm / MmPerFoot;
        }

        public static double MillimetersFromFeet(double feet)
        {
            return feet * MmPerFoot;
        }

        public static double FeetFromMeters(double meters)
        {
            return meters * FeetPerMeter;
        }

        public static double MetersFromFeet(double feet)
        {
            return feet / FeetPerMeter;
        }
    }
}
