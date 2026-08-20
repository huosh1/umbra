using Umbra.Core;

namespace Umbra.Tests;

public class BrowserBlockingStateTests : IDisposable
{
    private readonly string _tempDir;

    public BrowserBlockingStateTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "umbra-browser-tests-" + Guid.NewGuid());
        Config.DataDir = _tempDir;
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { }
    }

    [Fact]
    public void GetCurrent_ReturnsGlobalSitesDuringAFocusSession()
    {
        Blocklist.Save(new BlocklistData { Sites = new List<string> { "www.youtube.com", "reddit.com" } });
        Session.StartCustom(25, false, "test");

        var state = BrowserBlockingState.GetCurrent();
        Assert.True(state.Blocking);
        Assert.Equal(new[] { "reddit.com", "youtube.com" }, state.Sites);
    }

    [Fact]
    public void GetCurrent_ReturnsNoRulesOutsideBlockingTime()
    {
        Blocklist.Save(new BlocklistData { Sites = new List<string> { "youtube.com" } });
        Assert.False(BrowserBlockingState.GetCurrent().Blocking);
    }

    [Fact]
    public void GetCurrent_ReturnsAlwaysBlockedSites_EvenWithNoSessionOrPeriodActive()
    {
        AlwaysBlocklist.Save(new BlocklistData { Sites = new List<string> { "x.com", "tiktok.com" } });

        var state = BrowserBlockingState.GetCurrent();
        Assert.True(state.Blocking);
        Assert.Equal(new[] { "tiktok.com", "x.com" }, state.Sites);
    }
}
