using System;
using System.IO;
using System.Text.Json;

namespace Meet2Docs.Gui;

public class UserSettings
{
    public string UrlsText { get; set; } = "";
    public string SelectOnlyText { get; set; } = "";
    public bool NoDateFilter { get; set; }
    public string BeginHourText { get; set; } = "6";
    public string EndHourText { get; set; } = "22";

    private static readonly string SettingsDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Meet2Docs");

    private static readonly string SettingsPath =
        Path.Combine(SettingsDir, "settings.json");

    public static UserSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
                return JsonSerializer.Deserialize<UserSettings>(File.ReadAllText(SettingsPath)) ?? new();
        }
        catch { /* corrupt file – fall back to defaults */ }
        return new();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(SettingsDir);
            var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsPath, json);
        }
        catch { /* best-effort */ }
    }
}
