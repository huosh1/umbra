using System.Text.Json;

namespace Umbra.Core;

// Plus simple que côté Electron à dessein : pas de système de palettes CSS
// ni de particules à reconstruire, WinUI 3 fournit un vrai thème natif
// (clair/sombre/système) directement, et Mica remplace l'image/vidéo de
// fond csS-approximée.
public class AppSettings
{
    public string Language { get; set; } = "fr"; // "fr" | "en"
    public string Theme { get; set; } = "Dark"; // "Dark" | "Light"
    public List<int> DurationPresets { get; set; } = new() { 25, 60, 180 }; // minutes
    public string FocusClockStyle { get; set; } = "halo"; // halo | orbit | simple | arc | digital
    public bool PlayEndOfSessionSound { get; set; } = true;
    public bool PlayEndOfBreakSound { get; set; } = true;
    public bool ShowSpotifyTile { get; set; } = true;
    public string SmartReminderMode { get; set; } = "off"; // off | manual | automatic
    public string SmartReminderTime { get; set; } = "09:00";
    public string? BackgroundImagePath { get; set; } // null = pas d'image de fond (défaut)
    public List<string> RecentBackgroundImages { get; set; } = new(); // les plus récentes en premier
    public double BackgroundOverlayOpacity { get; set; } = 0.88; // 0 = fond très visible, 1 = comme sans image
    public string BackgroundAppearanceMode { get; set; } = "full"; // full | content | navigation
    public double BackgroundBlur { get; set; } = 80;
    public string? FloatingFocusBackgroundPath { get; set; }
    public List<string> RecentFloatingFocusBackgrounds { get; set; } = new();
    public double FloatingFocusBlur { get; set; } = 12;
}

public static class Settings
{
    private static AppSettings DefaultSettings() => new();

    public static AppSettings Load()
    {
        if (!File.Exists(Config.SettingsFile)) return DefaultSettings();
        try
        {
            var json = File.ReadAllText(Config.SettingsFile);
            return JsonSerializer.Deserialize<AppSettings>(json, Json.Options) ?? DefaultSettings();
        }
        catch
        {
            return DefaultSettings();
        }
    }

    public static void Save(AppSettings data)
    {
        File.WriteAllText(Config.SettingsFile, JsonSerializer.Serialize(data, Json.Options));
    }
}
