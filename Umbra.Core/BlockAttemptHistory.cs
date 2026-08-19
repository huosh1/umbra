using System.Text.Json;

namespace Umbra.Core;

public class BlockAttempt
{
    public string Target { get; set; } = "";
    public string Kind { get; set; } = "app";
    public int Count { get; set; }
    public long LastAttemptAt { get; set; }
}

public static class BlockAttemptHistory
{
    public static void Record(string target, string kind = "app")
    {
        if (string.IsNullOrWhiteSpace(target)) return;
        var data = Load();
        var entry = data.FirstOrDefault(item =>
            item.Kind == kind && string.Equals(item.Target, target, StringComparison.OrdinalIgnoreCase));
        if (entry is null)
        {
            entry = new BlockAttempt { Target = target.Trim(), Kind = kind };
            data.Add(entry);
        }
        entry.Count++;
        entry.LastAttemptAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        // Le navigateur envoie un "target" arbitraire via Native Messaging à
        // chaque tentative bloquée - une extension modifiée pourrait en
        // forger des milliers de distincts pour faire grossir ce fichier
        // sans limite. On ne garde que les plus pertinents (les plus
        // fréquents/récents), jamais un vrai usage ne s'approche de ce seuil.
        const int maxEntries = 500;
        if (data.Count > maxEntries)
        {
            data = data.OrderByDescending(item => item.Count).ThenByDescending(item => item.LastAttemptAt).Take(maxEntries).ToList();
        }
        AtomicFile.WriteAllText(Config.BlockAttemptsFile, JsonSerializer.Serialize(data, Json.Options));
    }

    public static List<BlockAttempt> GetTop(int count) =>
        Load().OrderByDescending(item => item.Count).ThenByDescending(item => item.LastAttemptAt).Take(count).ToList();

    public static int GetTotal() => Load().Sum(item => item.Count);

    private static List<BlockAttempt> Load()
    {
        if (!File.Exists(Config.BlockAttemptsFile)) return new();
        try
        {
            return JsonSerializer.Deserialize<List<BlockAttempt>>(File.ReadAllText(Config.BlockAttemptsFile), Json.Options) ?? new();
        }
        catch
        {
            return new();
        }
    }
}
