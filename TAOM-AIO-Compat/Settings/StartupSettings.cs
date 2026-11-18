// Reads the switches straight from MCM's saved file at start-up. MCM's settings instance is not ready
// when fixes have to be installed (SubModule constructor / OnSubModuleLoad), so every switch needs a
// restart and is read here once.
// File (MCM v5 "json2", Global settings): Documents\Mount and Blade II Bannerlord\Configs\ModSettings\
// Global\<FolderName>\<Id>.json, a flat object of property name -> value. Missing file or key = default.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using TAOM_AIO_Compat.Core;

namespace TAOM_AIO_Compat.Settings
{
    internal static class StartupSettings
    {
        private static Dictionary<string, bool> _values;

        internal static string FilePath =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "Mount and Blade II Bannerlord",
                "Configs", "ModSettings", "Global", AioSettings.SettingsFolder, AioSettings.SettingsId + ".json");

        internal static bool IsOn(string key, bool defaultValue = true)
        {
            Load();
            return _values.TryGetValue(key, out bool v) ? v : defaultValue;
        }

        private static void Load()
        {
            if (_values != null)
                return;
            _values = new Dictionary<string, bool>(StringComparer.Ordinal);
            string path = FilePath;
            try
            {
                if (!File.Exists(path))
                {
                    AioLog.Write("Settings file not found (first start?): every switch uses its default (on). " + path);
                    return;
                }
                string text = File.ReadAllText(path);
                foreach (Match m in Regex.Matches(text, "\"([A-Za-z0-9_]+)\"\\s*:\\s*(true|false)\\b", RegexOptions.IgnoreCase))
                    _values[m.Groups[1].Value] = string.Equals(m.Groups[2].Value, "true", StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception e)
            {
                AioLog.Write("Could not read settings file, every switch uses its default (on): " + e.Message);
            }
        }
    }
}
