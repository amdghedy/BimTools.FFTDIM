using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using Autodesk.Revit.UI;

namespace BimTools.FFTDIM.Core
{
    /// <summary>Report formats offered by the export commands.</summary>
    public enum ReportOutputFormat
    {
        Csv = 0,
        Html = 1,
        Json = 2
    }

    /// <summary>Languages available for the add-in UI.</summary>
    public enum InterfaceLanguage
    {
        English = 0,
        Arabic = 1
    }

    /// <summary>
    /// User-configurable options that shape every FFTDIM command. Persisted to
    /// %APPDATA%\BimTools\FFTDIM\settings.json. Missing members in an older settings
    /// file keep the default values assigned here, so new options are always safe
    /// even when the file on disk was written by a previous release.
    /// </summary>
    [DataContract]
    public class AppSettings
    {
        // ------------------------------------------------------------------
        //  Audit thresholds
        // ------------------------------------------------------------------

        /// <summary>Dimensions measuring less than this (in mm) are reported as zero-value. Default 0.5 mm.</summary>
        [DataMember]
        public double ZeroValueThresholdMm { get; set; } = 0.5;

        /// <summary>Include SpotDimension elements in scans (off by default — spot elevations and coordinates would otherwise flood duplicate/zero checks with noise).</summary>
        [DataMember]
        public bool IncludeSpotDimensions { get; set; } = false;

        /// <summary>Flag dimensions whose references resolve into a linked Revit model (their nested geometry is not analysed).</summary>
        [DataMember]
        public bool FlagLinkedReferences { get; set; } = true;

        // ------------------------------------------------------------------
        //  Quick dimension
        // ------------------------------------------------------------------

        /// <summary>Perpendicular offset of the spacing-chain dimension line, metres. Default 1.0 m.</summary>
        [DataMember]
        public double ChainOffsetMeters { get; set; } = 1.0;

        /// <summary>Overhang of the spacing-chain dimension line beyond the first/last element, metres. Default 0.3 m.</summary>
        [DataMember]
        public double ChainExtensionMeters { get; set; } = 0.3;

        /// <summary>Clearance dimensions closer than this are skipped as meaningless, mm. Default 50 mm.</summary>
        [DataMember]
        public double ClearanceSkipMm { get; set; } = 50.0;

        /// <summary>Maximum search radius when hunting for the nearest grid/wall target, metres. Default 10 m.</summary>
        [DataMember]
        public double ClearanceSearchRadiusMeters { get; set; } = 10.0;

        /// <summary>When true, clearance dimensions prefer walls even when a grid line is marginally closer.</summary>
        [DataMember]
        public bool PreferWallsOverGrids { get; set; } = false;

        // ------------------------------------------------------------------
        //  Reports and interface
        // ------------------------------------------------------------------

        /// <summary>Format pre-selected by the export commands.</summary>
        [DataMember]
        public ReportOutputFormat DefaultReportFormat { get; set; } = ReportOutputFormat.Csv;

        /// <summary>Language of windows and ribbon captions (ribbon picks it up on the next Revit start).</summary>
        [DataMember]
        public InterfaceLanguage Language { get; set; } = InterfaceLanguage.English;

        // ------------------------------------------------------------------
        //  Converted accessors (Revit internal unit is the foot)
        // ------------------------------------------------------------------

        public double ZeroValueThresholdFeet
        {
            get { return UnitHelper.FeetFromMillimeters(ZeroValueThresholdMm); }
        }

        public double ChainOffsetFeet
        {
            get { return UnitHelper.FeetFromMeters(ChainOffsetMeters); }
        }

        public double ChainExtensionFeet
        {
            get { return UnitHelper.FeetFromMeters(ChainExtensionMeters); }
        }

        public double ClearanceSkipFeet
        {
            get { return UnitHelper.FeetFromMillimeters(ClearanceSkipMm); }
        }

        public double ClearanceSearchRadiusFeet
        {
            get { return UnitHelper.FeetFromMeters(ClearanceSearchRadiusMeters); }
        }
    }

    /// <summary>
    /// Loads and saves <see cref="AppSettings"/> to %APPDATA%\BimTools\FFTDIM\settings.json
    /// using DataContractJsonSerializer (no external dependencies) — the same pattern as
    /// ToleranceRuleStore. A corrupt or unreadable file falls back to defaults.
    /// </summary>
    public static class AppSettingsStore
    {
        private static string DirectoryPath
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "BimTools", "FFTDIM");
            }
        }

        /// <summary>Full path of the settings file (shown in the settings window for discoverability).</summary>
        public static string FilePath
        {
            get { return Path.Combine(DirectoryPath, "settings.json"); }
        }

        public static AppSettings LoadOrDefaults()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    var serializer = new DataContractJsonSerializer(typeof(AppSettings));
                    using (FileStream stream = File.OpenRead(FilePath))
                    {
                        var loaded = serializer.ReadObject(stream) as AppSettings;
                        if (loaded != null) return loaded;
                    }
                }
            }
            catch
            {
                // Corrupt or unreadable settings file falls back to defaults; never block the command.
            }
            return new AppSettings();
        }

        public static void Save(AppSettings settings)
        {
            try
            {
                Directory.CreateDirectory(DirectoryPath);
                var serializer = new DataContractJsonSerializer(typeof(AppSettings));
                using (FileStream stream = File.Create(FilePath))
                {
                    serializer.WriteObject(stream, settings);
                }
            }
            catch (Exception ex)
            {
                TaskDialog.Show("FFTDIM", "Could not save settings:\n\n" + ex.Message +
                    "\n\nSettings file: " + FilePath);
            }
        }
    }
}
