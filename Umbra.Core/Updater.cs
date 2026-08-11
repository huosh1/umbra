using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Umbra.Core;

public sealed class UpdateCheckResult
{
    public bool CheckSucceeded { get; init; }
    public bool Available { get; init; }
    public string CurrentVersion { get; init; } = "";
    public string? LatestVersion { get; init; }
    public string? ReleaseUrl { get; init; }
    public string? Notes { get; init; }
    public string? InstallerName { get; init; }
    public string? InstallerUrl { get; init; }
    public string? ChecksumsUrl { get; init; }
    public long InstallerSize { get; init; }

    public bool CanInstall => Available
        && !string.IsNullOrWhiteSpace(InstallerName)
        && !string.IsNullOrWhiteSpace(InstallerUrl)
        && !string.IsNullOrWhiteSpace(ChecksumsUrl);
}

internal sealed class GitHubRelease
{
    [JsonPropertyName("tag_name")]
    public string? TagName { get; set; }

    [JsonPropertyName("html_url")]
    public string? HtmlUrl { get; set; }

    [JsonPropertyName("body")]
    public string? Body { get; set; }

    [JsonPropertyName("assets")]
    public List<GitHubAsset> Assets { get; set; } = [];
}

internal sealed class GitHubAsset
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("browser_download_url")]
    public string DownloadUrl { get; set; } = "";

    [JsonPropertyName("size")]
    public long Size { get; set; }
}

public static class Updater
{
    private const string Repo = "huosh1/umbra";
    private const string ChecksumsAssetName = "SHA256SUMS.txt";
    private static readonly string ApiUrl = $"https://api.github.com/repos/{Repo}/releases/latest";
    private static readonly Regex VersionPattern = new(@"\d+(?:\.\d+){1,3}", RegexOptions.Compiled);
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromMinutes(10) };

    static Updater()
    {
        Http.DefaultRequestHeaders.UserAgent.ParseAdd("Umbra-App-Updater");
        Http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
    }

    public static int CompareVersions(string a, string b)
    {
        var pa = ParseVersionParts(a);
        var pb = ParseVersionParts(b);
        for (var i = 0; i < Math.Max(pa.Length, pb.Length); i++)
        {
            var na = i < pa.Length ? pa[i] : 0;
            var nb = i < pb.Length ? pb[i] : 0;
            if (na != nb) return na.CompareTo(nb);
        }
        return 0;
    }

    public static async Task<UpdateCheckResult> CheckForUpdateAsync(string currentVersion, CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await Http.GetAsync(ApiUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode)
                return new UpdateCheckResult { CurrentVersion = currentVersion };

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var release = await JsonSerializer.DeserializeAsync<GitHubRelease>(stream, cancellationToken: cancellationToken);
            if (release?.TagName is null)
                return new UpdateCheckResult { CurrentVersion = currentVersion };

            var latestVersion = NormalizeVersion(release.TagName);
            var available = CompareVersions(latestVersion, currentVersion) > 0;
            var expectedInstallerName = $"Umbra-Setup-{latestVersion}-x64.exe";
            var installer = release.Assets.FirstOrDefault(asset =>
                string.Equals(asset.Name, expectedInstallerName, StringComparison.OrdinalIgnoreCase));
            var checksums = release.Assets.FirstOrDefault(asset =>
                string.Equals(asset.Name, ChecksumsAssetName, StringComparison.OrdinalIgnoreCase));

            return new UpdateCheckResult
            {
                CheckSucceeded = true,
                Available = available,
                CurrentVersion = currentVersion,
                LatestVersion = latestVersion,
                ReleaseUrl = release.HtmlUrl,
                Notes = release.Body ?? "",
                InstallerName = installer?.Name,
                InstallerUrl = installer?.DownloadUrl,
                ChecksumsUrl = checksums?.DownloadUrl,
                InstallerSize = installer?.Size ?? 0,
            };
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new UpdateCheckResult { CurrentVersion = currentVersion };
        }
        catch (HttpRequestException)
        {
            return new UpdateCheckResult { CurrentVersion = currentVersion };
        }
        catch (JsonException)
        {
            return new UpdateCheckResult { CurrentVersion = currentVersion };
        }
    }

    public static async Task<string> DownloadInstallerAsync(
        UpdateCheckResult update,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (!update.CanInstall || update.InstallerName is null || update.InstallerUrl is null || update.ChecksumsUrl is null)
            throw new InvalidOperationException("The release does not contain a verifiable Umbra installer.");
        if (!IsTrustedGitHubUrl(update.InstallerUrl) || !IsTrustedGitHubUrl(update.ChecksumsUrl))
            throw new InvalidOperationException("The release contains an untrusted download URL.");

        var checksumText = await Http.GetStringAsync(update.ChecksumsUrl, cancellationToken);
        if (!TryReadExpectedSha256(checksumText, update.InstallerName, out var expectedHash))
            throw new InvalidDataException("The installer checksum is missing from SHA256SUMS.txt.");

        var updatesDirectory = Path.Combine(Config.DataDir, "updates");
        Directory.CreateDirectory(updatesDirectory);
        var destinationPath = Path.Combine(updatesDirectory, update.InstallerName);
        var temporaryPath = destinationPath + ".download";

        try
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);

            using var response = await Http.GetAsync(update.InstallerUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();
            var totalBytes = response.Content.Headers.ContentLength ?? update.InstallerSize;

            await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
            var actualHash = await WriteVerifiedDownloadAsync(
                source,
                temporaryPath,
                totalBytes,
                progress,
                cancellationToken);
            if (!CryptographicOperations.FixedTimeEquals(
                    Convert.FromHexString(actualHash),
                    Convert.FromHexString(expectedHash)))
                throw new CryptographicException("The downloaded installer failed SHA-256 verification.");

            // The helper has disposed its FileShare.None destination before
            // this rename. Keeping that stream alive makes Move fail on
            // Windows with a sharing violation.
            File.Move(temporaryPath, destinationPath, overwrite: true);
            progress?.Report(1);
            return destinationPath;
        }
        catch
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
            throw;
        }
    }

    internal static async Task<string> WriteVerifiedDownloadAsync(
        Stream source,
        string temporaryPath,
        long totalBytes,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        await using var destination = new FileStream(
            temporaryPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 1024 * 128,
            options: FileOptions.Asynchronous | FileOptions.SequentialScan);
        using var sha256 = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

        var buffer = new byte[1024 * 128];
        long downloadedBytes = 0;
        var lastReportedPercent = -1;
        while (true)
        {
            var bytesRead = await source.ReadAsync(buffer, cancellationToken);
            if (bytesRead == 0) break;
            await destination.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
            sha256.AppendData(buffer, 0, bytesRead);
            downloadedBytes += bytesRead;

            if (totalBytes > 0)
            {
                var percent = (int)Math.Clamp(downloadedBytes * 100 / totalBytes, 0, 100);
                if (percent != lastReportedPercent)
                {
                    lastReportedPercent = percent;
                    progress?.Report(percent / 100d);
                }
            }
        }

        await destination.FlushAsync(cancellationToken);
        return Convert.ToHexString(sha256.GetHashAndReset()).ToLowerInvariant();
    }

    internal static bool TryReadExpectedSha256(string contents, string fileName, out string expectedHash)
    {
        expectedHash = "";
        foreach (var line in contents.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var match = Regex.Match(line, @"^(?<hash>[0-9a-fA-F]{64})\s+\*?(?<name>.+)$");
            if (!match.Success || !string.Equals(match.Groups["name"].Value.Trim(), fileName, StringComparison.OrdinalIgnoreCase))
                continue;

            expectedHash = match.Groups["hash"].Value.ToLowerInvariant();
            return true;
        }
        return false;
    }

    internal static bool IsTrustedGitHubUrl(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
            return false;
        return uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase)
            || uri.Host.EndsWith(".githubusercontent.com", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeVersion(string value) =>
        VersionPattern.Match(value).Value is { Length: > 0 } version ? version : "0.0.0";

    private static int[] ParseVersionParts(string value)
    {
        var normalized = NormalizeVersion(value);
        return normalized.Split('.').Select(part => int.TryParse(part, out var number) ? number : 0).ToArray();
    }
}
