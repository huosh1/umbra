using Umbra.Core;

namespace Umbra.Tests;

public class MusicHistoryTests : IDisposable
{
    private readonly string _tempDir;

    public MusicHistoryTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "umbra-music-tests-" + Guid.NewGuid());
        Config.DataDir = _tempDir;
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { }
    }

    [Fact]
    public void RecordPlayback_CountsOnlySamplesMarkedAsNewPlays()
    {
        MusicHistory.RecordPlayback("Orbit", "Weightlessness", 3, countAsPlay: true);
        MusicHistory.RecordPlayback("Orbit", "Weightlessness", 3);
        MusicHistory.RecordPlayback("Orbit", "Weightlessness", 3);

        var track = Assert.Single(MusicHistory.GetTopTracks(5));
        Assert.Equal(1, track.PlayCount);
        Assert.Equal(9, track.Seconds);
    }

    [Fact]
    public void RecordPlayback_StoresAndRefreshesArtwork()
    {
        MusicHistory.RecordPlayback("Orbit", "Weightlessness", 3, new byte[] { 1, 2 });
        MusicHistory.RecordPlayback("Orbit", "Weightlessness", 3, new byte[] { 3, 4 });

        Assert.Equal(new byte[] { 3, 4 }, Assert.Single(MusicHistory.GetTopTracks(5)).Thumbnail);
    }
}
