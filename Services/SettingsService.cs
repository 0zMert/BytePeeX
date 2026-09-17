using System;
using System.IO;
using System.Text.Json;

namespace Folderize.Services
{
    public class SettingsModel
    {
        public string Language { get; set; } = "tr";
        public bool HideSystemInTreemap { get; set; } = false;
        public bool UseBalancedTreemapScale { get; set; } = true;
    }

    public class SettingsService
    {
        public static SettingsService Instance { get; } = new();

        private readonly string _configFilePath;
        public SettingsModel Settings { get; private set; }

        public SettingsService()
        {
            Settings = new SettingsModel();
            try
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string folder = Path.Combine(appData, "Folderize");
                if (!Directory.Exists(folder))
                {
                    Directory.CreateDirectory(folder);
                }
                _configFilePath = Path.Combine(folder, "config.json");

                if (File.Exists(_configFilePath))
                {
                    string json = File.ReadAllText(_configFilePath);
                    var loaded = JsonSerializer.Deserialize<SettingsModel>(json);
                    if (loaded != null)
                    {
                        Settings = loaded;
                    }
                }
            }
            catch
            {
                _configFilePath = "";
            }
        }

        public void Save()
        {
            try
            {
                if (!string.IsNullOrEmpty(_configFilePath))
                {
                    var options = new JsonSerializerOptions { WriteIndented = true };
                    string json = JsonSerializer.Serialize(Settings, options);
                    File.WriteAllText(_configFilePath, json);
                }
            }
            catch
            {
            }
        }
    }
}
