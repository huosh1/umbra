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
