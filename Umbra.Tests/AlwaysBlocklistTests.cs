using Umbra.Core;

namespace Umbra.Tests;

public class AlwaysBlocklistTests : IDisposable
{
    private readonly string _tempDir;

    public AlwaysBlocklistTests()
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
        var data = AlwaysBlocklist.Load();
        Assert.Empty(data.Apps);
        Assert.Empty(data.Sites);
    }

    [Fact]
    public void SaveThenLoad_RoundTripsAppsAndSites()
    {
        var data = new BlocklistData
        {
            Apps = new List<string> { "steam.exe" },
            Sites = new List<string> { "tiktok.com", "x.com" },
        };
        AlwaysBlocklist.Save(data);

        var loaded = AlwaysBlocklist.Load();
        Assert.Equal(data.Apps, loaded.Apps);
        Assert.Equal(data.Sites, loaded.Sites);
    }

    [Fact]
    public void SaveThenLoad_IsIndependentFromTheSessionBlocklist()
    {
        Blocklist.Save(new BlocklistData { Sites = new List<string> { "reddit.com" } });
        AlwaysBlocklist.Save(new BlocklistData { Sites = new List<string> { "tiktok.com" } });

        Assert.Equal(new[] { "reddit.com" }, Blocklist.Load().Sites);
        Assert.Equal(new[] { "tiktok.com" }, AlwaysBlocklist.Load().Sites);
    }

    [Fact]
    public void Load_ReturnsEmptyLists_WhenFileIsCorrupted()
    {
        File.WriteAllText(Config.AlwaysBlocklistFile, "{ not valid json");
        var data = AlwaysBlocklist.Load();
        Assert.Empty(data.Apps);
        Assert.Empty(data.Sites);
    }
}
