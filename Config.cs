using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace CleanRecentMini
{
    internal class Config
    {
        public string Language { get; set; } = "en-US";
        public bool AutoStart { get; set; } = false;
        public bool IncognitoMode { get; set; } = false;

        public static readonly List<LanguageInfo> SupportedLanguages = new List<LanguageInfo>
        {
            new LanguageInfo { Code = "en-US", DisplayName = "English" },
            new LanguageInfo { Code = "zh-CN", DisplayName = "中文(简体)" }
        };

        private static readonly string ConfigPath = Path.Combine(
            Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location),
            "config.json");

        public static Config Load()
        {
            if (File.Exists(ConfigPath))
            {
                try
                {
                    string json = File.ReadAllText(ConfigPath);
                    return JsonSerializer.Deserialize<Config>(json);
                }
                catch
                {
                    return CreateDefaultConfig();
                }
            }

            return CreateDefaultConfig();
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
                string json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(ConfigPath, json);
            }
            catch
            {
                // failed to save config file, ingore failure
            }
        }
    }

    public class LanguageInfo
    {
        public string Code { get; set; }
        public string DisplayName { get; set; }
    }
}
