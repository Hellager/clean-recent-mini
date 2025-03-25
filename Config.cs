using System.IO;
using System.Text.Json;

namespace CleanRecentMini
{
    internal class Config
    {
        public string Language { get; set; } = "en-US";
        public bool AutoStart { get; set; } = false;
        public bool IncognitoMode { get; set; } = false;

        private static readonly string ConfigPath = Path.Combine(
            Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location),
            "config.json");

        public static Config Load()
        {
            if (File.Exists(ConfigPath))
            {
                string json = File.ReadAllText(ConfigPath);
                return JsonSerializer.Deserialize<Config>(json);
            }

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
            string json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(ConfigPath, json);
        }
    }
}
