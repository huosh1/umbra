using Umbra.Core;

namespace Umbra.Tests;

public sealed class UpdateReadinessTests
{
    private static readonly DateTime Now = new(2026, 8, 11, 14, 30, 0);

    [Fact]
    public void ActiveSessionPostponesUpdate()
    {
        var reason = UpdateReadiness.Evaluate(
            new SessionState { Active = true },
            new PeriodsData(),
            Now);

        Assert.Equal(UpdateBlockReason.ActiveSession, reason);
    }

    [Fact]
    public void ActiveSchedulePostponesUpdate()
    {
        var schedule = new Period
        {
            Enabled = true,
            Recurring = true,
            Days = [(int)Now.DayOfWeek],
            StartTime = "14:00",
            EndTime = "15:00",
        };

        var reason = UpdateReadiness.Evaluate(
            new SessionState(),
            new PeriodsData { Periods = [schedule] },
            Now);

        Assert.Equal(UpdateBlockReason.ActiveSchedule, reason);
    }

    [Fact]
    public void InactiveSessionAndScheduleAllowUpdate()
    {
        var schedule = new Period
        {
            Enabled = true,
            Recurring = true,
            Days = [(int)Now.DayOfWeek],
            StartTime = "16:00",
            EndTime = "17:00",
        };

        var reason = UpdateReadiness.Evaluate(
            new SessionState(),
            new PeriodsData { Periods = [schedule] },
            Now);

        Assert.Equal(UpdateBlockReason.None, reason);
    }
}
