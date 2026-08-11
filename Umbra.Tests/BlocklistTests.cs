using System.Text.Json;
using Umbra.Core;

namespace Umbra.Tests;

public class BlocklistTests : IDisposable
{
    private readonly string _tempDir;

    public BlocklistTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "umbra-tests-" + Guid.NewGuid());
        Config.DataDir = _tempDir;
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { }
    }

    [Fact]
    public void Load_ReturnsEmptyLists_WhenFileDoesNotExist()
    {
        var data = Blocklist.Load();
        Assert.Empty(data.Apps);
        Assert.Empty(data.Sites);
    }

    [Fact]
    public void SaveThenLoad_RoundTripsAppsAndSites()
    {
        var data = new BlocklistData
        {
            Apps = new List<string> { "chrome.exe", "steam.exe" },
            Sites = new List<string> { "twitch.tv", "youtube.com" },
        };
        Blocklist.Save(data);

        var loaded = Blocklist.Load();
        Assert.Equal(data.Apps, loaded.Apps);
        Assert.Equal(data.Sites, loaded.Sites);
    }

    [Fact]
    public void Load_MigratesWebsiteEntriesStoredAsApplications()
    {
        File.WriteAllText(Config.BlocklistFile, """
            {
              "apps": ["Discord.exe", "youtube.com", "https://www.twitch.tv/videos/123"],
              "sites": ["reddit.com"]
            }
            """);

        var loaded = Blocklist.Load();

        Assert.Equal(new[] { "Discord.exe" }, loaded.Apps);
        Assert.Equal(new[] { "reddit.com", "youtube.com", "twitch.tv" }, loaded.Sites);

        using var persisted = JsonDocument.Parse(File.ReadAllText(Config.BlocklistFile));
        var persistedApps = persisted.RootElement.GetProperty("apps").EnumerateArray().Select(item => item.GetString()).ToList();
        Assert.DoesNotContain("youtube.com", persistedApps);
        Assert.DoesNotContain("twitch.tv", persistedApps);
    }

    [Theory]
    [InlineData("youtube.com", "youtube.com")]
    [InlineData("https://www.youtube.com/watch?v=abc", "youtube.com")]
    [InlineData("*.twitch.tv", "twitch.tv")]
    public void NormalizeSiteInput_ReturnsAHostOnly(string input, string expected)
    {
        Assert.Equal(expected, Blocklist.NormalizeSiteInput(input));
    }

    [Fact]
    public void SavedProfiles_MigrateWebsiteEntriesStoredAsApplications()
    {
        File.WriteAllText(Config.SavedBlocklistsFile, """
            [{ "name": "Focused", "apps": ["notepad.exe", "twitch.tv"], "sites": ["x.com"] }]
            """);

        var profile = Assert.Single(SavedBlocklists.Load());

        Assert.Equal(new[] { "notepad.exe" }, profile.Apps);
        Assert.Equal(new[] { "x.com", "twitch.tv" }, profile.Sites);

        using var persisted = JsonDocument.Parse(File.ReadAllText(Config.SavedBlocklistsFile));
        var persistedApps = persisted.RootElement[0].GetProperty("apps").EnumerateArray().Select(item => item.GetString()).ToList();
        Assert.DoesNotContain("twitch.tv", persistedApps);
    }

    [Fact]
    public void Load_ReturnsEmptyLists_WhenFileIsCorrupted()
    {
        File.WriteAllText(Config.BlocklistFile, "{ not valid json");
        var data = Blocklist.Load();
        Assert.Empty(data.Apps);
        Assert.Empty(data.Sites);
    }

    [Fact]
    public void BuiltInPresets_HaveUniqueKeysAndNonEmptySites()
    {
        Assert.Equal(6, BlocklistPresets.All.Length);
        Assert.Equal(BlocklistPresets.All.Length, BlocklistPresets.All.Select(p => p.Key).Distinct().Count());
        Assert.All(BlocklistPresets.All, preset => Assert.All(preset.Sites, site => Assert.False(string.IsNullOrWhiteSpace(site))));
    }
}
