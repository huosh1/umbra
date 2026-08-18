using System.Text.Json;

namespace Umbra.Core;

public class Period
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public bool Enabled { get; set; }
    public bool Recurring { get; set; } = true;
    public List<int> Days { get; set; } = new();
    public string? Date { get; set; } // null = pas encore normalisé (voir Periods.Load)
    public string? PausedDate { get; set; }
    public string StartTime { get; set; } = "00:00";
    public string EndTime { get; set; } = "00:00";
    public List<string> Apps { get; set; } = new();
    public List<string> Sites { get; set; } = new();
    // Comme Session.HardMode, mais pour une plage : empêche de la
    // désactiver ou de la supprimer tant qu'elle est activement en train de
    // bloquer (PeriodCoversNow=true) - sinon le mode hard d'une session
    // manuelle est contournable en désactivant simplement la plage à la
    // place. Ne restreint rien en dehors de sa fenêtre horaire active.
    public bool HardMode { get; set; }
    // Pendant que la plage bloque activement, alterne 25 min concentration /
    // 5 min pause au lieu d'un simple compte à rebours jusqu'à la fin -
    // voir Periods.GetPomodoroTiming.
    public bool PomodoroMode { get; set; }
}

public class PeriodsData
{
    public List<Period> Periods { get; set; } = new();
}

public static class Periods
{
    public static string TodayKey(DateTime now) => now.ToString("yyyy-MM-dd");

    // Une plage ponctuelle ("aujourd'hui seulement") n'a plus de raison
    // d'être gardée une fois sa date passée - on la retire au chargement
    // pour ne pas laisser periods.json grossir indéfiniment ni fausser
    // HasEnabledPeriod().
    public static PeriodsData Load()
    {
        if (!File.Exists(Config.PeriodsFile)) return new PeriodsData();
        try
        {
            var json = File.ReadAllText(Config.PeriodsFile);
            var data = JsonSerializer.Deserialize<PeriodsData>(json, Json.Options) ?? new PeriodsData();
            var today = TodayKey(DateTime.Now);
            foreach (var p in data.Periods)
            {
                p.Date ??= today;
            }
            data.Periods = data.Periods.Where(p => p.Recurring || string.CompareOrdinal(p.Date, today) >= 0).ToList();
            return data;
        }
        catch
        {
            return new PeriodsData();
        }
    }

    public static void Save(PeriodsData data)
    {
        File.WriteAllText(Config.PeriodsFile, JsonSerializer.Serialize(data, Json.Options));
    }

    private static int TimeToMinutes(string? hhmm)
    {
        var parts = (hhmm ?? "00:00").Split(':');
        var h = parts.Length > 0 && int.TryParse(parts[0], out var hv) ? hv : 0;
        var m = parts.Length > 1 && int.TryParse(parts[1], out var mv) ? mv : 0;
        return h * 60 + m;
    }

    public static bool PeriodCoversNow(Period p, DateTime now)
    {
        if (!p.Enabled) return false;
        // Coupure d'urgence pour aujourd'hui seulement (voir "Désactiver
        // aujourd'hui" côté UI) : ne touche pas à la config (jours/horaires),
        // donc une plage récurrente reprend normalement le jour suivant sans
        // rien devoir réactiver à la main.
        if (p.PausedDate == TodayKey(now)) return false;

        var mins = now.Hour * 60 + now.Minute;
        var start = TimeToMinutes(p.StartTime);
        var end = TimeToMinutes(p.EndTime);
        if (start == end) return false;

        if (p.Recurring)
        {
            if (!p.Days.Contains((int)now.DayOfWeek)) return false; // DayOfWeek : 0=dimanche..6=samedi, comme Date.getDay() en JS
            if (start < end) return mins >= start && mins < end;
            return mins >= start || mins < end; // plage récurrente qui traverse minuit (ex: 22:00 -> 02:00)
        }
        // plage ponctuelle : bornée au jour choisi, pas de traversée de minuit
        if (p.Date != TodayKey(now)) return false;
        return start < end && mins >= start && mins < end;
    }

    // Minutes restantes avant la fin d'une plage actuellement active (pour
    // remonter un temps restant à l'extension navigateur, comme pour une
    // session classique).
    public static int MinutesUntilEnd(Period p, DateTime now)
    {
        var mins = now.Hour * 60 + now.Minute;
        var end = TimeToMinutes(p.EndTime);
        if (end > mins) return end - mins;
        return 1440 - mins + end; // la fin est passée minuit (plage récurrente uniquement)
    }

    // true si au moins une plage activée couvre l'instant donné.
    public static bool IsActiveNow(PeriodsData data, DateTime now) =>
        data.Periods.Any(p => PeriodCoversNow(p, now));

    // Les plages actuellement actives, pour récupérer leurs listes de
    // blocage propres (apps/sites par période).
    public static List<Period> GetActivePeriods(PeriodsData data, DateTime now) =>
        data.Periods.Where(p => PeriodCoversNow(p, now)).ToList();

    public static bool HasEnabledPeriod(PeriodsData data) =>
        data.Periods.Any(p => p.Enabled);

    // Temps restant/total d'une plage actuellement active, pour alimenter le
    // même anneau de progression que les sessions manuelles (Focus et
    // fenêtre flottante) - gère la traversée de minuit comme
    // PeriodCoversNow. Ne suppose pas que la plage est active : appelant
    // responsable de l'avoir vérifié via PeriodCoversNow au préalable.
    public static (double RemainingSeconds, double TotalSeconds) GetTiming(Period period, DateTime now)
    {
        if (!TimeSpan.TryParse(period.StartTime, out var startTime) || !TimeSpan.TryParse(period.EndTime, out var endTime))
            return (0, 1);
        var start = now.Date + startTime;
        var end = now.Date + endTime;
        if (end <= start)
        {
            if (now < end) start = start.AddDays(-1); else end = end.AddDays(1);
        }
        return (Math.Max(0, (end - now).TotalSeconds), Math.Max(1, (end - start).TotalSeconds));
    }

    // Comme GetTiming, mais pour une plage en PomodoroMode : découpe le temps
    // écoulé depuis le début de la plage en cycles classiques 25/5 qui se
    // répètent jusqu'à la fin de la plage - pas de "cycles total" fixe comme
    // une session manuelle (Session.StartPomodoro), c'est la fin de la plage
    // elle-même qui borne/tronque le dernier cycle. Stateless comme GetTiming :
    // recalculé à chaque appel à partir de l'heure de début, rien à persister.
    public static (bool IsBreak, double RemainingSeconds, double TotalSeconds, int Cycle) GetPomodoroTiming(Period period, DateTime now)
    {
        if (!TimeSpan.TryParse(period.StartTime, out var startTime) || !TimeSpan.TryParse(period.EndTime, out var endTime))
            return (false, 0, 1, 1);
        var start = now.Date + startTime;
        var end = now.Date + endTime;
        if (end <= start)
        {
            if (now < end) start = start.AddDays(-1); else end = end.AddDays(1);
        }

        const double focusSeconds = 25 * 60, breakSeconds = 5 * 60, cycleSeconds = focusSeconds + breakSeconds;
        var elapsed = Math.Max(0, (now - start).TotalSeconds);
        var cycleIndex = (int)(elapsed / cycleSeconds);
        var posInCycle = elapsed - cycleIndex * cycleSeconds;
        var isBreak = posInCycle >= focusSeconds;
        var phaseTotal = isBreak ? breakSeconds : focusSeconds;
        var phaseElapsed = isBreak ? posInCycle - focusSeconds : posInCycle;
        var remaining = phaseTotal - phaseElapsed;

        // La phase en cours ne doit jamais déborder après la fin de la plage
        // (pas de pause qui continuerait alors que le planning est terminé).
        var untilPeriodEnd = (end - now).TotalSeconds;
        if (remaining > untilPeriodEnd) remaining = Math.Max(0, untilPeriodEnd);

        return (isBreak, remaining, phaseTotal, cycleIndex + 1);
    }
}
