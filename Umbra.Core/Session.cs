using System.Text.Json;
using System.Text.Json.Serialization;

namespace Umbra.Core;

public class PomodoroState
{
    [JsonPropertyName("workMinutes")]
    public int WorkMinutes { get; set; }

    [JsonPropertyName("breakMinutes")]
    public int BreakMinutes { get; set; }

    [JsonPropertyName("cyclesTotal")]
    public int CyclesTotal { get; set; }

    [JsonPropertyName("cycleIndex")]
    public int CycleIndex { get; set; }

    [JsonPropertyName("phase")]
    public string Phase { get; set; } = "work"; // "work" | "break"
}

public class SessionState
{
    [JsonPropertyName("active")]
    public bool Active { get; set; }

    [JsonPropertyName("kind")]
    public string Kind { get; set; } = "custom"; // "custom" | "pomodoro"

    [JsonPropertyName("hardMode")]
    public bool HardMode { get; set; }

    [JsonPropertyName("startTs")]
    public long StartTs { get; set; }

    [JsonPropertyName("endTs")]
    public long EndTs { get; set; }

    [JsonPropertyName("questName")]
    public string QuestName { get; set; } = "";

    [JsonPropertyName("pomodoro")]
    public PomodoroState? Pomodoro { get; set; }
}

public static class Session
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private static long NowMs => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    public static void Save(SessionState s)
    {
        AtomicFile.WriteAllText(Config.SessionFile, JsonSerializer.Serialize(s, JsonOptions));
    }

    // Fait avancer une session pomodoro dont la phase en cours est terminée
    // (travail -> pause -> travail suivant, ou fin du dernier cycle -> session
    // terminée). Appelée depuis Load() : quel que soit qui la charge en
    // premier (watchdog ou dashboard), l'état avance de façon cohérente sur
    // la même horloge murale, sans double-décompte ni désync entre process.
    private static SessionState AdvancePomodoroIfDue(SessionState s)
    {
        if (s.Kind != "pomodoro" || !s.Active || NowMs < s.EndTs) return s;
        var p = s.Pomodoro!;
        if (p.Phase == "work")
        {
            if (p.CycleIndex + 1 >= p.CyclesTotal)
            {
                s.Active = false;
                Save(s);
                History.Append("pomodoro", s.HardMode, s.QuestName, p.CyclesTotal * p.WorkMinutes);
                return s;
            }
            p.Phase = "break";
            s.StartTs = s.EndTs;
            s.EndTs = s.StartTs + p.BreakMinutes * 60000;
        }
        else
        {
            p.CycleIndex += 1;
            p.Phase = "work";
            s.StartTs = s.EndTs;
            s.EndTs = s.StartTs + p.WorkMinutes * 60000;
        }
        Save(s);
        return AdvancePomodoroIfDue(s); // au cas où plusieurs phases seraient dues d'un coup (watchdog resté éteint longtemps)
    }

    public static SessionState Load()
    {
        if (!File.Exists(Config.SessionFile)) return new SessionState();
        SessionState s;
        try
        {
            var json = File.ReadAllText(Config.SessionFile);
            s = JsonSerializer.Deserialize<SessionState>(json) ?? new SessionState();
        }
        catch
        {
            return new SessionState();
        }
        return AdvancePomodoroIfDue(s);
    }

    public static SessionState StartCustom(double durationMinutes, bool hardMode, string questName)
    {
        var now = NowMs;
        var s = new SessionState
        {
            Active = true,
            Kind = "custom",
            HardMode = hardMode,
            StartTs = now,
            EndTs = now + (long)(durationMinutes * 60 * 1000),
            QuestName = questName,
        };
        Save(s);
        return s;
    }

    public static SessionState StartPomodoro(int workMinutes, int breakMinutes, int cyclesTotal, bool hardMode, string questName)
    {
        var now = NowMs;
        var s = new SessionState
        {
            Active = true,
            Kind = "pomodoro",
            HardMode = hardMode,
            StartTs = now,
            EndTs = now + workMinutes * 60000,
            QuestName = questName,
            Pomodoro = new PomodoroState { WorkMinutes = workMinutes, BreakMinutes = breakMinutes, CyclesTotal = cyclesTotal, CycleIndex = 0, Phase = "work" },
        };
        Save(s);
        return s;
    }

    public static double RemainingSeconds(SessionState s)
    {
        if (!s.Active) return 0;
        return Math.Max(0, (s.EndTs - NowMs) / 1000.0);
    }

    // Le blocage doit être actif pendant une session "custom", ou pendant la
    // phase "travail" d'un pomodoro - pas pendant sa pause.
    public static bool IsBlockingActive(SessionState s)
    {
        if (!s.Active) return false;
        if (s.Kind == "pomodoro") return s.Pomodoro!.Phase == "work";
        return true;
    }

    public static bool CanStop(SessionState s)
    {
        if (!s.Active) return true;
        if (!s.HardMode) return true;
        return RemainingSeconds(s) <= 0;
    }

    // Temps réellement passé en phase "travail", même pour un arrêt manuel en
    // cours de route (pas juste la durée programmée) - c'est ça qui doit
    // remonter dans l'historique/les stats.
    private static double ComputeFocusedMinutes(SessionState s)
    {
        if (s.Kind == "custom")
        {
            var elapsed = (NowMs - s.StartTs) / 60000.0;
            var scheduled = (s.EndTs - s.StartTs) / 60000.0;
            return Math.Max(0, Math.Min(elapsed, scheduled));
        }
        var p = s.Pomodoro!;
        if (p.Phase == "work")
        {
            var partial = Math.Max(0, Math.Min((NowMs - s.StartTs) / 60000.0, p.WorkMinutes));
            return p.CycleIndex * p.WorkMinutes + partial;
        }
        return (p.CycleIndex + 1) * p.WorkMinutes; // en pause : le cycle de travail qui y a mené est complet
    }

    public static SessionState Stop(SessionState s)
    {
        if (s.Active)
        {
            History.Append(s.Kind, s.HardMode, s.QuestName, ComputeFocusedMinutes(s));
        }
        s.Active = false;
        Save(s);
        return s;
    }
}
