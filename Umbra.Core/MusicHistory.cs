using System.Text.Json;

namespace Umbra.Core;

public class TrackPlayTime
{
    public string Title { get; set; } = "";
    public string Artist { get; set; } = "";
    public double Seconds { get; set; }
    public byte[]? Thumbnail { get; set; }
    public int PlayCount { get; set; }
}

// Cumul du temps d'écoute Spotify PENDANT les sessions de focus - alimenté
// par NowPlayingBar (tick de 3s, ajoute 3s au morceau en cours si une
// session est active et que ça joue réellement). Approximatif par nature
// (basé sur un sondage périodique, pas un vrai suivi début/fin de lecture)
// mais suffisant pour un classement "les plus écoutés".
public static class MusicHistory
{
    private static readonly Mutex HistoryMutex = new(false, "Local\\UmbraNative.MusicHistory");

    public static void RecordPlayback(string title, string artist, double seconds, byte[]? thumbnail = null, bool countAsPlay = false)
    {
        if (string.IsNullOrWhiteSpace(title)) return;
        EnterHistoryLock();
        try
        {
            var data = Load();
            var entry = data.FirstOrDefault(t => t.Title == title && t.Artist == artist);
            if (entry is null)
            {
                entry = new TrackPlayTime { Title = title, Artist = artist };
                data.Add(entry);
            }
            entry.Seconds += seconds;
            if (countAsPlay) entry.PlayCount += 1;
            if (thumbnail is { Length: > 0 }) entry.Thumbnail = thumbnail;
            Save(data);
        }
        finally { HistoryMutex.ReleaseMutex(); }
    }

    public static List<TrackPlayTime> GetTopTracks(int count) => WithHistoryLock(() =>
        Load().OrderByDescending(t => t.Seconds).Take(count).ToList());

    public static List<TrackPlayTime> GetAllTracks() => WithHistoryLock(() =>
        Load().OrderByDescending(t => t.Seconds).ToList());

    private static T WithHistoryLock<T>(Func<T> action)
    {
        EnterHistoryLock();
        try { return action(); }
        finally { HistoryMutex.ReleaseMutex(); }
    }

    private static void EnterHistoryLock()
    {
        try { HistoryMutex.WaitOne(); }
        catch (AbandonedMutexException) { }
    }

    private static List<TrackPlayTime> Load()
    {
        if (!File.Exists(Config.MusicHistoryFile)) return new List<TrackPlayTime>();
        try
        {
            var json = File.ReadAllText(Config.MusicHistoryFile);
            return JsonSerializer.Deserialize<List<TrackPlayTime>>(json, Json.Options) ?? new List<TrackPlayTime>();
        }
        catch
        {
            return new List<TrackPlayTime>();
        }
    }

    private static void Save(List<TrackPlayTime> data)
    {
        File.WriteAllText(Config.MusicHistoryFile, JsonSerializer.Serialize(data, Json.Options));
    }
}
