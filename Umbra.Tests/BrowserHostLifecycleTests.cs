using Umbra.Core;

namespace Umbra.Tests;

public sealed class BrowserHostLifecycleTests : IDisposable
{
    private readonly string _tempDir;

    public BrowserHostLifecycleTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "umbra-browser-host-tests-" + Guid.NewGuid());
        Config.DataDir = _tempDir;
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { }
    }

    [Fact]
    public void StopRequestPersistsUntilItIsCleared()
    {
        Assert.False(BrowserHostLifecycle.IsStopRequested());

        Assert.True(BrowserHostLifecycle.RequestStop());
        Assert.True(BrowserHostLifecycle.IsStopRequested());

        BrowserHostLifecycle.ClearStopRequest();
        Assert.False(BrowserHostLifecycle.IsStopRequested());
    }
}
