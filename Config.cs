using System.Collections.Generic;
using System.IO;
using Tomlyn;
using Tomlyn.Model;

namespace CleanRecentMini
{
    internal class Config
    {
        public string Language { get; set; } = "en-US";
        public bool AutoStart { get; set; } = false;
        public bool IncognitoMode { get; set; } = false;
        public bool QueryFeasible { get; set; } = true;
        public bool HandleFeasible { get; set; } = true;

        public static readonly List<LanguageInfo> SupportedLanguages = new List<LanguageInfo>
        {
            new LanguageInfo { Code = "en-US", DisplayName = "English" },
            new LanguageInfo { Code = "zh-CN", DisplayName = "中文" },
        };

        private static readonly string ConfigPath = Path.Combine(
            Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location),
            "config.toml");

        public static Config Load()
        {
            if (File.Exists(ConfigPath))
            {
                try
                {
                    string tomlString = File.ReadAllText(ConfigPath);
                    var tomlTable = Toml.ToModel(tomlString);

                    return new Config
                    {
                        Language = GetValueOrDefault<string>(tomlTable, "Language", "en-US"),
                        AutoStart = GetValueOrDefault<bool>(tomlTable, "AutoStart", false),
                        IncognitoMode = GetValueOrDefault<bool>(tomlTable, "IncognitoMode", false),
                        QueryFeasible = GetValueOrDefault<bool>(tomlTable, "QueryFeasible", true),
                        HandleFeasible = GetValueOrDefault<bool>(tomlTable, "HandleFeasible", true)
                    };
                }
                catch
                {
                    return CreateDefaultConfig();
                }
            }

            return CreateDefaultConfig();
        }

        private static T GetValueOrDefault<T>(TomlTable table, string key, T defaultValue)
        {
            if (table.ContainsKey(key) && table[key] is T value)
            {
                return value;
            }
            return defaultValue;
        }

        private static Config CreateDefaultConfig()
        {
            var config = new Config();
            if (System.Globalization.CultureInfo.CurrentUICulture.Name.StartsWith("zh"))
            {
                config.Language = "zh-CN";
            }

            Save(config);
            return config;
        }

        public static void Save(Config config)
        {
            try
            {
                var tomlTable = new TomlTable
                {
                    ["Language"] = config.Language,
                    ["AutoStart"] = config.AutoStart,
                    ["IncognitoMode"] = config.IncognitoMode,
                    ["QueryFeasible"] = config.QueryFeasible,
                    ["HandleFeasible"] = config.HandleFeasible
                };

                string tomlString = Toml.FromModel(tomlTable);
                File.WriteAllText(ConfigPath, tomlString);
            }
            catch
            {
                // failed to save config file, ignore failure
            }
        }
    }

    public class LanguageInfo
    {
        public string Code { get; set; }
        public string DisplayName { get; set; }
    }
}
