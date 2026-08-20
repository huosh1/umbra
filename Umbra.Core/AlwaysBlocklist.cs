using System.Text.Json;

namespace Umbra.Core;

// Blocage permanent, indépendant de toute session/plage - "je ne veux
// jamais pouvoir aller sur X", pas "seulement pendant que je travaille".
// Même forme de données/normalisation que Blocklist (fichier hosts + kill
// process, apps/sites gérés pareil), juste une source de blocage de plus
// que WatchdogLoop applique à chaque tick, sans condition. Fichier séparé
// de blocklist.json pour ne jamais mélanger accidentellement la liste
// "pendant les sessions" (modifiable librement) et celle-ci (censée rester
// stable).
public static class AlwaysBlocklist
{
    public static BlocklistData Load()
    {
        if (!File.Exists(Config.AlwaysBlocklistFile)) return new BlocklistData();
        try
        {
            var json = File.ReadAllText(Config.AlwaysBlocklistFile);
            var data = JsonSerializer.Deserialize<BlocklistData>(json, Json.Options) ?? new BlocklistData();
            return Blocklist.Normalize(data);
        }
        catch
        {
            return new BlocklistData();
        }
    }

    public static void Save(BlocklistData data)
    {
        AtomicFile.WriteAllText(Config.AlwaysBlocklistFile, JsonSerializer.Serialize(Blocklist.Normalize(data), Json.Options));
    }
}
