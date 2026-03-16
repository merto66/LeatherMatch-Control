using System.IO;
using System.Text.Json;
using LeatherMatchControl.Models;

namespace LeatherMatchControl.Services;

public class SettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _configPath;

    public SettingsService()
    {
        var baseDir = AppContext.BaseDirectory;
        _configPath = Path.Combine(baseDir, "config.json");
    }

    public AppSettings Load()
    {
        if (!File.Exists(_configPath))
        {
            var defaults = new AppSettings();
            Save(defaults);
            return defaults;
        }

        try
        {
            var json = File.ReadAllText(_configPath);
            return JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
        }
        catch
        {
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        var json = JsonSerializer.Serialize(settings, JsonOptions);
        File.WriteAllText(_configPath, json);
    }
}
