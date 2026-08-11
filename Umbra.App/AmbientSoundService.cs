using System.IO;
using System.Windows.Media;

namespace Umbra.App;

public sealed record AmbientSound(string Id, string Name, string AudioFile, string ImageFile);
public sealed record ActiveAmbientSound(AmbientSound Sound, double Volume);

public static class AmbientSoundService
{
    private sealed class PlayerState(AmbientSound sound, MediaPlayer player)
    {
        public AmbientSound Sound { get; } = sound;
        public MediaPlayer Player { get; } = player;
    }

    public const int MaxActive = 3;
    private static readonly Dictionary<string, PlayerState> Players = new(StringComparer.OrdinalIgnoreCase);
    public static event Action? Changed;

    public static IReadOnlyList<AmbientSound> Catalog { get; } =
    [
        new("birds", "Birds", "birds.wav", "birds.png"),
        new("waterfall", "Waterfall", "waterfall.wav", "waterfall.png"),
        new("coffeeshop", "Coffee Shop", "coffeeshop.mp3", "coffeeshop.png"),
        new("wind", "Wind", "wind.wav", "wind.png"),
        new("creek", "Creek", "creek.wav", "creek.png"),
        new("beach", "Beach", "beach.wav", "beach.png"),
        new("underwater", "Underwater", "underwater.wav", "underwater.png"),
        new("citystreet", "City Street", "citystreet.wav", "citystreet.png"),
        new("rain", "Rain", "rain.wav", "rain.png"),
        new("rainforest", "Rainforest", "Rainforest.mp3", "rainforest.png"),
        new("whitenoise", "White Noise", "whitenoise.wav", "whitenoise.png"),
        new("thunder", "Thunder", "thunder.mp3", "thunder.png"),
        new("fireplace", "Fireplace", "fireplace.wav", "fireplace.png")
    ];

    public static IReadOnlyList<ActiveAmbientSound> Active => Players.Values
        .Select(x => new ActiveAmbientSound(x.Sound, x.Player.Volume)).ToList();

    public static string ImagePath(AmbientSound sound) =>
        Path.Combine(AppContext.BaseDirectory, "Assets", "Ambient", "Images", sound.ImageFile);

    public static bool Toggle(AmbientSound sound)
    {
        if (Players.Remove(sound.Id, out var existing))
        {
            existing.Player.Close();
            Changed?.Invoke();
            return true;
        }
        if (Players.Count >= MaxActive) return false;

        var player = new MediaPlayer { Volume = 0.55 };
        var path = Path.Combine(AppContext.BaseDirectory, "Assets", "Ambient", "Sounds", sound.AudioFile);
        player.Open(new Uri(path, UriKind.Absolute));
        player.MediaEnded += (_, _) => { player.Position = TimeSpan.Zero; player.Play(); };
        Players[sound.Id] = new PlayerState(sound, player);
        player.Play();
        Changed?.Invoke();
        return true;
    }

    public static bool IsActive(string id) => Players.ContainsKey(id);

    public static void SetVolume(string id, double volume)
    {
        if (!Players.TryGetValue(id, out var state)) return;
        state.Player.Volume = Math.Clamp(volume, 0, 1);
    }

    public static void Remove(string id)
    {
        if (!Players.Remove(id, out var state)) return;
        state.Player.Close();
        Changed?.Invoke();
    }
}
