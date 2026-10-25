using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using Autodesk.Revit.UI;

namespace BimTools.FFTDIM.Core
{
    /// <summary>
    /// A user-editable tolerance rule. Values are stored in metres (user-facing) and converted
    /// to Revit internal units (feet) when checked. MinMeters / MaxMeters of 0 mean "no limit".
    ///
    /// Scope semantics:
    ///   "Sprinkler" — dimensions whose references include at least one sprinkler;
    ///   "Pipe"      — dimensions referencing pipes / pipe fittings / pipe insulation;
    ///   "All"       — any dimension that references model geometry.
    /// A dimension is checked against every enabled rule whose scope matches.
    /// </summary>
    [DataContract]
    public class ToleranceRule
    {
        [DataMember]
        public string Name { get; set; }

        [DataMember]
        public string Scope { get; set; }

        [DataMember]
        public double MinMeters { get; set; }

        [DataMember]
        public double MaxMeters { get; set; }

        [DataMember]
        public bool Enabled { get; set; }
    }

    /// <summary>
    /// Loads and saves tolerance rules to %APPDATA%\BimTools\FFTDIM\tolerance_rules.json
    /// using the framework's DataContractJsonSerializer (no external dependencies).
    /// </summary>
    public static class ToleranceRuleStore
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

        private static string FilePath
        {
            get { return Path.Combine(DirectoryPath, "tolerance_rules.json"); }
        }

        public static List<ToleranceRule> LoadOrDefaults()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    var serializer = new DataContractJsonSerializer(typeof(List<ToleranceRule>));
                    using (FileStream stream = File.OpenRead(FilePath))
                    {
                        var loaded = serializer.ReadObject(stream) as List<ToleranceRule>;
                        if (loaded != null && loaded.Count > 0) return loaded;
                    }
                }
            }
            catch
            {
                // Corrupt or unreadable settings file falls back to defaults; never block the command.
            }
            return DefaultRules();
        }

        public static void Save(List<ToleranceRule> rules)
        {
            try
            {
                Directory.CreateDirectory(DirectoryPath);
                var serializer = new DataContractJsonSerializer(typeof(List<ToleranceRule>));
                using (FileStream stream = File.Create(FilePath))
                {
                    serializer.WriteObject(stream, rules);
                }
            }
            catch (Exception ex)
            {
                TaskDialog.Show("FFTDIM", "Could not save tolerance rules:\n\n" + ex.Message +
                    "\n\nSettings file: " + FilePath);
            }
        }

        /// <summary>True when at least one rule is enabled and has a configured limit.</summary>
        public static bool HasEnabledRules(List<ToleranceRule> rules)
        {
            foreach (ToleranceRule rule in rules)
            {
                if (rule.Enabled && (rule.MinMeters > 0 || rule.MaxMeters > 0)) return true;
            }
            return false;
        }

        /// <summary>
        /// Factory defaults. These are sensible starting points, NOT code requirements:
        /// the sprinkler spacing envelope is a common light-hazard layout range and must be
        /// adapted to the project's applicable standard before being relied upon.
        /// </summary>
        public static List<ToleranceRule> DefaultRules()
        {
            return new List<ToleranceRule>
            {
                new ToleranceRule
                {
                    Name = "Sprinkler spacing (layout envelope)",
                    Scope = "Sprinkler",
                    MinMeters = 1.8,
                    MaxMeters = 4.5,
                    Enabled = true
                },
                new ToleranceRule
                {
                    Name = "Example: max pipe run between dims (edit or disable)",
                    Scope = "Pipe",
                    MinMeters = 0,
                    MaxMeters = 12,
                    Enabled = false
                },
                new ToleranceRule
                {
                    Name = "Example: any dimension sanity limit",
                    Scope = "All",
                    MinMeters = 0,
                    MaxMeters = 30,
                    Enabled = false
                }
            };
        }
    }
}
