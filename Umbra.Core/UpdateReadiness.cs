namespace Umbra.Core;

public enum UpdateBlockReason
{
    None,
    ActiveSession,
    ActiveSchedule,
}

public static class UpdateReadiness
{
    public static UpdateBlockReason Evaluate(SessionState session, PeriodsData periods, DateTime now)
    {
        if (session.Active) return UpdateBlockReason.ActiveSession;
        if (Periods.IsActiveNow(periods, now)) return UpdateBlockReason.ActiveSchedule;
        return UpdateBlockReason.None;
    }
}
