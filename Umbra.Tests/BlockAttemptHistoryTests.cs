using Umbra.Core;

namespace Umbra.Tests;

public class BlockAttemptHistoryTests : IDisposable
{
    private readonly string _tempDir;

    public BlockAttemptHistoryTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "umbra-attempt-tests-" + Guid.NewGuid());
        Config.DataDir = _tempDir;
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { }
    }

    [Fact]
    public void Record_AggregatesAttemptsAndSortsTheTop()
    {
        BlockAttemptHistory.Record("discord.exe");
        BlockAttemptHistory.Record("steam.exe");
        BlockAttemptHistory.Record("discord.exe");

        var top = BlockAttemptHistory.GetTop(5);
        Assert.Equal(3, BlockAttemptHistory.GetTotal());
        Assert.Equal("discord.exe", top[0].Target);
        Assert.Equal(2, top[0].Count);
    }
}
