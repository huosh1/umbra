using System.Text.Json;
using System.Text.Json.Serialization;

namespace Umbra.Core;

public class UpdateCheckResult
{
    public bool Available { get; set; }
    public string CurrentVersion { get; set; } = "";
    public string? LatestVersion { get; set; }
    public string? Url { get; set; }
    public string? Notes { get; set; }
}

internal class GitHubRelease
{
    [JsonPropertyName("tag_name")]
    public string? TagName { get; set; }

    [JsonPropertyName("html_url")]
    public string? HtmlUrl { get; set; }

    public string? Body { get; set; }
}

public static class Updater
{
    // À ajuster une fois un dépôt dédié à la version native créé (voir le
    // plan : la bascule réelle est une décision séparée, prise plus tard).
    private const string Repo = "huosh1/umbra";
    private static readonly string ApiUrl = $"https://api.github.com/repos/{Repo}/releases/latest";

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(8) };

    static Updater()
    {
        Http.DefaultRequestHeaders.UserAgent.ParseAdd("Umbra-App");
    }

    public static int CompareVersions(string a, string b)
    {
        var pa = a.Split('.').Select(s => int.TryParse(s, out var n) ? n : 0).ToArray();
        var pb = b.Split('.').Select(s => int.TryParse(s, out var n) ? n : 0).ToArray();
        for (var i = 0; i < Math.Max(pa.Length, pb.Length); i++)
        {
            var na = i < pa.Length ? pa[i] : 0;
            var nb = i < pb.Length ? pb[i] : 0;
            if (na != nb) return na - nb;
        }
        return 0;
    }

    // null si pas de release publiée ou erreur réseau - jamais fatal,
    // l'appli doit continuer à fonctionner normalement sans connexion.
    private static async Task<GitHubRelease?> FetchLatestReleaseAsync()
    {
        try
        {
            using var res = await Http.GetAsync(ApiUrl);
            if (!res.IsSuccessStatusCode) return null;
            var json = await res.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<GitHubRelease>(json);
        }
        catch
        {
            return null;
        }
    }

    public static async Task<UpdateCheckResult> CheckForUpdateAsync(string currentVersion)
    {
        var release = await FetchLatestReleaseAsync();
        if (release?.TagName == null) return new UpdateCheckResult { Available = false, CurrentVersion = currentVersion };

        var latestVersion = System.Text.RegularExpressions.Regex.Replace(release.TagName, "^v", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        var available = CompareVersions(latestVersion, currentVersion) > 0;
        return new UpdateCheckResult
        {
            Available = available,
            CurrentVersion = currentVersion,
            LatestVersion = latestVersion,
            Url = release.HtmlUrl,
            Notes = release.Body ?? "",
        };
    }
}
