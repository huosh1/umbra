using System.Text.Json;

namespace Umbra.Core;

// Listes préfaites courantes (sites uniquement - les apps varient trop d'un
// PC à l'autre pour être devinées à l'avance) - la clé sert de clé de
// traduction côté UI (Umbra.App/Loc.cs), le Core reste agnostique du texte
// affiché.
public record BuiltInBlocklist(string Key, string[] Sites);

public static class BlocklistPresets
{
    public static readonly BuiltInBlocklist[] All =
    {
        new("social", new[] { "facebook.com", "instagram.com", "x.com", "twitter.com", "tiktok.com", "reddit.com", "snapchat.com" }),
        new("streaming", new[] { "netflix.com", "youtube.com", "twitch.tv", "primevideo.com", "disneyplus.com" }),
        new("games", new[] { "store.steampowered.com", "epicgames.com", "chess.com", "leagueoflegends.com" }),
        new("news", new[] { "lemonde.fr", "lefigaro.fr", "bfmtv.com", "franceinfo.fr", "cnn.com", "bbc.com" }),
        new("shopping", new[] { "amazon.fr", "ebay.fr", "aliexpress.com", "shein.com", "temu.com" }),
        new("messaging", new[] { "discord.com", "web.whatsapp.com", "messenger.com", "web.telegram.org" }),
    };
}

public class SavedBlocklist
{
    public string Name { get; set; } = "";
    public List<string> Apps { get; set; } = new();
    public List<string> Sites { get; set; } = new();
}

// Profils de blocage nommés que l'utilisateur peut enregistrer/recharger -
// séparé de BlocklistData (la liste "active") pour ne jamais perdre un
// preset en modifiant la liste en cours.
public static class SavedBlocklists
{
    public static List<SavedBlocklist> Load()
    {
        if (!File.Exists(Config.SavedBlocklistsFile)) return new List<SavedBlocklist>();
        try
        {
            var json = File.ReadAllText(Config.SavedBlocklistsFile);
            return JsonSerializer.Deserialize<List<SavedBlocklist>>(json, Json.Options) ?? new List<SavedBlocklist>();
        }
        catch
        {
            return new List<SavedBlocklist>();
        }
    }

    public static void Save(List<SavedBlocklist> lists)
    {
        File.WriteAllText(Config.SavedBlocklistsFile, JsonSerializer.Serialize(lists, Json.Options));
    }
}
