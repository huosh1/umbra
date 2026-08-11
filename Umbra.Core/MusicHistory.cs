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

// Tracks media playback during focus sessions. The in-memory cache matters:
// artwork makes the JSON file several megabytes large, while playback is
// sampled every three seconds. Reloading and rewriting that entire file on
// every sample caused regular UI stalls as the history grew.
public static class MusicHistory
{
    private static readonly Mutex HistoryMutex = new(false, "Local\\UmbraNative.MusicHistory");
    private static readonly TimeSpan PersistInterval = TimeSpan.FromSeconds(30);
    private static List<TrackPlayTime>? _cachedData;
    private static string? _cachedPath;
    private static DateTime _cachedFileWriteUtc;
    private static DateTime _lastPersistedUtc;
    private static bool _dirty;
    private static long _revision;

    public static long Revision => Interlocked.Read(ref _revision);

    public static void RecordPlayback(string title, string artist, double seconds, byte[]? thumbnail = null, bool countAsPlay = false)
    {
        if (string.IsNullOrWhiteSpace(title)) return;
        EnterHistoryLock();
        try
        {
            var data = LoadCached();
            var entry = data.FirstOrDefault(t => t.Title == title && t.Artist == artist);
            if (entry is null)
            {
                entry = new TrackPlayTime { Title = title, Artist = artist };
                data.Add(entry);
            }

            entry.Seconds += seconds;
            if (countAsPlay) entry.PlayCount += 1;
            if (thumbnail is { Length: > 0 }) entry.Thumbnail = thumbnail;

            _dirty = true;
            Interlocked.Increment(ref _revision);

            // Persist track changes immediately. Ordinary time samples are
            // batched and flushed on exit, reducing disk work by roughly 10x.
            if (countAsPlay || DateTime.UtcNow - _lastPersistedUtc >= PersistInterval)
                PersistCached();
        }
        finally
        {
            HistoryMutex.ReleaseMutex();
        }
    }

    public static List<TrackPlayTime> GetTopTracks(int count) => WithHistoryLock(() =>
        LoadCached().OrderByDescending(t => t.Seconds).Take(count).Select(Clone).ToList());

    public static List<TrackPlayTime> GetAllTracks() => WithHistoryLock(() =>
        LoadCached().OrderByDescending(t => t.Seconds).Select(Clone).ToList());

    public static void Flush() => WithHistoryLock(() =>
    {
        PersistCached();
        return true;
    });

    private static T WithHistoryLock<T>(Func<T> action)
    {
        EnterHistoryLock();
        try
        {
            return action();
        }
        finally
        {
            HistoryMutex.ReleaseMutex();
        }
    }

    private static void EnterHistoryLock()
    {
        try
        {
            HistoryMutex.WaitOne();
        }
        catch (AbandonedMutexException)
        {
        }
    }

    private static List<TrackPlayTime> LoadCached()
    {
        var path = Config.MusicHistoryFile;
        if (_cachedData is not null && string.Equals(_cachedPath, path, StringComparison.OrdinalIgnoreCase))
        {
            var writeUtc = File.Exists(path) ? File.GetLastWriteTimeUtc(path) : DateTime.MinValue;
            if (_dirty || writeUtc <= _cachedFileWriteUtc) return _cachedData;
        }

        _cachedPath = path;
        _cachedData = LoadFromDisk(path);
        _cachedFileWriteUtc = File.Exists(path) ? File.GetLastWriteTimeUtc(path) : DateTime.MinValue;
        _lastPersistedUtc = DateTime.UtcNow;
        _dirty = false;
        Interlocked.Increment(ref _revision);
        return _cachedData;
    }

    private static List<TrackPlayTime> LoadFromDisk(string path)
    {
        if (!File.Exists(path)) return new List<TrackPlayTime>();
        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<List<TrackPlayTime>>(json, Json.Options) ?? new List<TrackPlayTime>();
        }
        catch
        {
            return new List<TrackPlayTime>();
        }
    }

    private static void PersistCached()
    {
        if (!_dirty || _cachedData is null || string.IsNullOrWhiteSpace(_cachedPath)) return;

        var temporaryPath = _cachedPath + ".tmp";
        File.WriteAllText(temporaryPath, JsonSerializer.Serialize(_cachedData, Json.Options));
        File.Move(temporaryPath, _cachedPath, overwrite: true);
        _cachedFileWriteUtc = File.GetLastWriteTimeUtc(_cachedPath);
        _lastPersistedUtc = DateTime.UtcNow;
        _dirty = false;
    }

    private static TrackPlayTime Clone(TrackPlayTime track) => new()
    {
        Title = track.Title,
        Artist = track.Artist,
        Seconds = track.Seconds,
        Thumbnail = track.Thumbnail,
        PlayCount = track.PlayCount,
    };
}
