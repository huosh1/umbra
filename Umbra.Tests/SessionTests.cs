using System.Text.Json;
using Umbra.Core;

namespace Umbra.Tests;

// Chaque test s'exécute sur un dossier temporaire dédié (nouvelle instance
// de la classe = nouveau dossier, comportement par défaut de xUnit) - même
// principe d'isolation que stub-electron.js côté JS.
public class SessionTests : IDisposable
{
    private readonly string _tempDir;

    public SessionTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "umbra-tests-" + Guid.NewGuid());
        Config.DataDir = _tempDir;
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { }
    }

    private static void ResetHistory() => History.Save(new List<HistoryEntry>());
    private static List<HistoryEntry> ReadHistory() => History.Load();

    [Fact]
    public void IsBlockingActive_TrueForActiveCustomSession()
    {
        var s = Umbra.Core.Session.StartCustom(30, false, "quest");
        Assert.True(Umbra.Core.Session.IsBlockingActive(s));
    }

    [Fact]
    public void IsBlockingActive_TrueDuringPomodoroWork_FalseDuringBreak()
    {
        var s = Umbra.Core.Session.StartPomodoro(25, 5, 2, false, "q");
        Assert.True(Umbra.Core.Session.IsBlockingActive(s));
        s.Pomodoro!.Phase = "break";
        Assert.False(Umbra.Core.Session.IsBlockingActive(s));
    }

    [Fact]
    public void IsBlockingActive_FalseWhenNoSessionActive()
    {
        var s = Umbra.Core.Session.Stop(Umbra.Core.Session.StartCustom(5, false, "q"));
        Assert.False(Umbra.Core.Session.IsBlockingActive(s));
    }

    [Fact]
    public void CanStop_HardModeBlocksUntilTimeUp_SoftModeNeverBlocks()
    {
        var s = Umbra.Core.Session.StartCustom(30, true, "quest"); // hard mode, 30 min left
        Assert.False(Umbra.Core.Session.CanStop(s));

        s.EndTs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - 1000; // deja termine
        Assert.True(Umbra.Core.Session.CanStop(s));

        var soft = Umbra.Core.Session.StartCustom(30, false, "quest");
        Assert.True(Umbra.Core.Session.CanStop(soft));
    }

    [Fact]
    public void Stop_OnActiveSession_LogsFocusedMinutesToHistory()
    {
        ResetHistory();
        var s = Umbra.Core.Session.StartCustom(60, false, "test-quest");
        s.StartTs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - 10 * 60000; // demarree il y a 10 min
        Umbra.Core.Session.Stop(s);

        var entries = ReadHistory();
        Assert.Single(entries);
        Assert.Equal("custom", entries[0].Kind);
        Assert.Equal("test-quest", entries[0].QuestName);
        // ~10 minutes ecoulees, avec un peu de marge pour le temps d'execution du test
        Assert.InRange(entries[0].FocusedMinutes, 9.5, 10.5);
    }

    [Fact]
    public void Stop_OnAlreadyInactiveSession_DoesNotLogDuplicate()
    {
        ResetHistory();
        var s = Umbra.Core.Session.StartCustom(60, false, "q");
        s.StartTs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - 5 * 60000; // 5 min ecoulees
        Umbra.Core.Session.Stop(s);
        Assert.Single(ReadHistory());

        Umbra.Core.Session.Stop(s); // s.Active est deja false ici - ne doit rien ajouter de plus
        Assert.Single(ReadHistory());
    }

    [Fact]
    public void VeryShortSession_UnderLoggingThreshold_IsNotRecorded()
    {
        ResetHistory();
        var s = Umbra.Core.Session.StartCustom(60, false, "q");
        s.StartTs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(); // aucun temps ecoule
        Umbra.Core.Session.Stop(s);
        Assert.Empty(ReadHistory());
    }

    [Fact]
    public void Load_AdvancesPomodoroSessionWhoseCurrentPhaseHasEnded()
    {
        var s = Umbra.Core.Session.StartPomodoro(25, 5, 2, false, "q");
        s.EndTs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - 1000; // cycle de travail cense termine
        Umbra.Core.Session.Save(s);

        var loaded = Umbra.Core.Session.Load();
        Assert.Equal("break", loaded.Pomodoro!.Phase);
        Assert.True(loaded.Active);
    }

    [Fact]
    public void Load_EndsSessionAfterLastPomodoroCycleCompletes()
    {
        ResetHistory();
        var s = Umbra.Core.Session.StartPomodoro(25, 5, 1, false, "last-cycle");
        s.EndTs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - 1000; // dernier (et unique) cycle termine
        Umbra.Core.Session.Save(s);

        var loaded = Umbra.Core.Session.Load();
        Assert.False(loaded.Active);
        var entries = ReadHistory();
        Assert.Single(entries);
        Assert.Equal("pomodoro", entries[0].Kind);
    }
}
