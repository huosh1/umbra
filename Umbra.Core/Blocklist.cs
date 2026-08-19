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
            var data = JsonSerializer.Deserialize<BlocklistData>(json, Json.Options) ?? new BlocklistData();
            var originalApps = data.Apps?.ToList();
            var originalSites = data.Sites?.ToList();
            Normalize(data);
            if (!SameValues(originalApps, data.Apps) || !SameValues(originalSites, data.Sites))
                AtomicFile.WriteAllText(Config.BlocklistFile, JsonSerializer.Serialize(data, Json.Options));
            return data;
        }
        catch
        {
            return new BlocklistData();
        }
    }

    public static void Save(BlocklistData data)
    {
        AtomicFile.WriteAllText(Config.BlocklistFile, JsonSerializer.Serialize(Normalize(data), Json.Options));
    }

    public static BlocklistData Normalize(BlocklistData data)
    {
        var apps = new List<string>();
        var sites = new List<string>();
        var appSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var siteSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void AddSite(string? value)
        {
            var site = NormalizeSiteInput(value);
            if (site.Length > 0 && siteSet.Add(site)) sites.Add(site);
        }

        foreach (var value in data.Sites ?? new List<string>()) AddSite(value);
        foreach (var value in data.Apps ?? new List<string>())
        {
            if (TryNormalizeWebsiteInput(value, out var site))
            {
                if (siteSet.Add(site)) sites.Add(site);
                continue;
            }

            var app = (value ?? "").Trim();
            if (app.Length > 0 && appSet.Add(app)) apps.Add(app);
        }

        data.Apps = apps;
        data.Sites = sites;
        return data;
    }

    public static bool TryNormalizeWebsiteInput(string? value, out string site)
    {
        site = "";
        var input = (value ?? "").Trim();
        if (input.Length == 0) return false;

        var explicitUrl = input.Contains("://", StringComparison.Ordinal)
            || input.StartsWith("www.", StringComparison.OrdinalIgnoreCase)
            || input.Contains('/')
            || input.Contains('?')
            || input.Contains('#');
        var normalized = NormalizeSiteInput(input);
        if (normalized.Length == 0 || normalized.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) return false;

        var hostType = Uri.CheckHostName(normalized);
        var looksLikeDomain = hostType != UriHostNameType.Unknown && normalized.Contains('.');
        if (!explicitUrl && !looksLikeDomain) return false;

        site = normalized;
        return true;
    }

    public static string NormalizeSiteInput(string? value)
    {
        var input = (value ?? "").Trim();
        if (input.Length == 0) return "";
        if (input.StartsWith("*.", StringComparison.Ordinal)) input = input[2..];

        var candidate = input.Contains("://", StringComparison.Ordinal) ? input : $"https://{input}";
        if (Uri.TryCreate(candidate, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
            && !string.IsNullOrWhiteSpace(uri.IdnHost))
        {
            var host = uri.IdnHost.TrimEnd('.').ToLowerInvariant();
            return host.StartsWith("www.", StringComparison.Ordinal) ? host[4..] : host;
        }

        input = input.TrimEnd('.').ToLowerInvariant();
        return input.StartsWith("www.", StringComparison.Ordinal) ? input[4..] : input;
    }

    private static bool SameValues(List<string>? original, List<string>? normalized) =>
        original is not null && normalized is not null && original.SequenceEqual(normalized, StringComparer.Ordinal);
}
