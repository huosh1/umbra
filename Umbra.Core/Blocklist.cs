using System.Text.Json;

namespace Umbra.Core;

public class BlocklistData
{
    public List<string> Apps { get; set; } = new();
    public List<string> Sites { get; set; } = new();
}

public static class Blocklist
{
    public static BlocklistData Load()
    {
        if (!File.Exists(Config.BlocklistFile)) return new BlocklistData();
        try
        {
            var json = File.ReadAllText(Config.BlocklistFile);
            return JsonSerializer.Deserialize<BlocklistData>(json, Json.Options) ?? new BlocklistData();
        }
        catch
        {
            return new BlocklistData();
        }
    }

    public static void Save(BlocklistData data)
    {
        File.WriteAllText(Config.BlocklistFile, JsonSerializer.Serialize(data, Json.Options));
    }
}
