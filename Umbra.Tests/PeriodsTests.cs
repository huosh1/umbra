using Umbra.Core;

namespace Umbra.Tests;

public class PeriodsTests
{
    private static string TodayKey(DateTime d) => Periods.TodayKey(d);
    private static string Hhmm(DateTime d) => d.ToString("HH:mm");

    private static Period MakePeriod(bool enabled, bool recurring, List<int>? days = null, string? date = null, string startTime = "00:00", string endTime = "00:00", string? pausedDate = null) => new()
    {
        Id = "p1",
        Name = "test",
        Enabled = enabled,
        Recurring = recurring,
        Days = days ?? new List<int>(),
        Date = date,
        StartTime = startTime,
        EndTime = endTime,
        PausedDate = pausedDate,
    };

    [Fact]
    public void RecurringPeriod_ActiveOnRightDayAndTimeWindow()
    {
        var now = DateTime.Now;
        var start = now.AddMinutes(-10);
        var end = now.AddMinutes(10);
        var p = MakePeriod(true, true, days: new List<int> { (int)now.DayOfWeek }, startTime: Hhmm(start), endTime: Hhmm(end));
        Assert.Single(Periods.GetActivePeriods(new PeriodsData { Periods = new List<Period> { p } }, now));
    }

    [Fact]
    public void RecurringPeriod_InactiveOnDifferentDay()
    {
        var now = DateTime.Now;
        var wrongDay = ((int)now.DayOfWeek + 1) % 7;
        var start = now.AddMinutes(-10);
        var end = now.AddMinutes(10);
        var p = MakePeriod(true, true, days: new List<int> { wrongDay }, startTime: Hhmm(start), endTime: Hhmm(end));
        Assert.Empty(Periods.GetActivePeriods(new PeriodsData { Periods = new List<Period> { p } }, now));
    }

    [Fact]
    public void RecurringPeriod_CrossingMidnight()
    {
        var now = new DateTime(2026, 1, 5, 23, 30, 0); // lundi 5 janv 2026, 23:30
        var p = MakePeriod(true, true, days: new List<int> { (int)now.DayOfWeek }, startTime: "22:00", endTime: "02:00");
        Assert.Single(Periods.GetActivePeriods(new PeriodsData { Periods = new List<Period> { p } }, now));

        var outsideWindow = new DateTime(2026, 1, 5, 12, 0, 0);
        Assert.Empty(Periods.GetActivePeriods(new PeriodsData { Periods = new List<Period> { p } }, outsideWindow));
    }

    [Fact]
    public void OneOffPeriod_ActiveOnlyToday_WithinWindow_NoMidnightCrossing()
    {
        var now = new DateTime(2026, 8, 11, 12, 0, 0);
        var start = now.AddMinutes(-10);
        var end = now.AddMinutes(10);
        var p = MakePeriod(true, false, date: TodayKey(now), startTime: Hhmm(start), endTime: Hhmm(end));
        Assert.Single(Periods.GetActivePeriods(new PeriodsData { Periods = new List<Period> { p } }, now));
    }

    [Fact]
    public void OneOffPeriod_InactiveOnDifferentDate_EvenWithMatchingTimeWindow()
    {
        var now = DateTime.Now;
        var start = now.AddMinutes(-10);
        var end = now.AddMinutes(10);
        var yesterday = now.AddDays(-1);
        var p = MakePeriod(true, false, date: TodayKey(yesterday), startTime: Hhmm(start), endTime: Hhmm(end));
        Assert.Empty(Periods.GetActivePeriods(new PeriodsData { Periods = new List<Period> { p } }, now));
    }

    [Fact]
    public void DisabledPeriod_NeverActive_RegardlessOfDayTime()
    {
        var now = DateTime.Now;
        var start = now.AddMinutes(-10);
        var end = now.AddMinutes(10);
        var p = MakePeriod(false, true, days: new List<int> { (int)now.DayOfWeek }, startTime: Hhmm(start), endTime: Hhmm(end));
        Assert.Empty(Periods.GetActivePeriods(new PeriodsData { Periods = new List<Period> { p } }, now));
    }

    [Fact]
    public void PausedDateForToday_SuppressesOtherwiseActivePeriod_WithoutTouchingConfig()
    {
        var now = DateTime.Now;
        var start = now.AddMinutes(-10);
        var end = now.AddMinutes(10);
        var p = MakePeriod(true, true, days: new List<int> { (int)now.DayOfWeek }, startTime: Hhmm(start), endTime: Hhmm(end), pausedDate: TodayKey(now));
        Assert.Empty(Periods.GetActivePeriods(new PeriodsData { Periods = new List<Period> { p } }, now));

        // Le lendemain, la pause ne s'applique plus (elle ne vaut que pour la date exacte enregistrée)
        var tomorrow = now.AddDays(1);
        var pTomorrow = MakePeriod(true, true, days: new List<int> { (int)tomorrow.DayOfWeek }, startTime: Hhmm(start), endTime: Hhmm(end), pausedDate: TodayKey(now));
        Assert.Single(Periods.GetActivePeriods(new PeriodsData { Periods = new List<Period> { pTomorrow } }, tomorrow));
    }

    [Fact]
    public void MinutesUntilEnd_ComputesRemainingTime_IncludingAcrossMidnight()
    {
        var now = new DateTime(2026, 1, 5, 15, 30, 0);
        var p = new Period { StartTime = "14:00", EndTime = "16:00" };
        Assert.Equal(30, Periods.MinutesUntilEnd(p, now));

        var pOvernight = new Period { StartTime = "22:00", EndTime = "02:00" };
        var lateNight = new DateTime(2026, 1, 5, 23, 0, 0);
        Assert.Equal(180, Periods.MinutesUntilEnd(pOvernight, lateNight)); // 23h00 -> 02h00 = 3h
    }

    [Fact]
    public void GetTiming_ComputesRemainingAndTotal_WithinSameDay()
    {
        var now = new DateTime(2026, 1, 5, 15, 0, 0);
        var p = new Period { StartTime = "14:00", EndTime = "16:00" };
        var (remaining, total) = Periods.GetTiming(p, now);
        Assert.Equal(3600, remaining); // 1h restante
        Assert.Equal(7200, total); // 2h de plage totale
    }

    [Fact]
    public void GetTiming_CrossingMidnight_ComputesFromTheCorrectStartDay()
    {
        var p = new Period { StartTime = "22:00", EndTime = "02:00" };

        var lateNight = new DateTime(2026, 1, 5, 23, 0, 0); // avant minuit : la plage a demarre aujourd'hui
        var (remainingLate, totalLate) = Periods.GetTiming(p, lateNight);
        Assert.Equal(10800, remainingLate); // 23h -> 02h = 3h restantes
        Assert.Equal(14400, totalLate); // 22h -> 02h = 4h au total

        var earlyMorning = new DateTime(2026, 1, 6, 1, 0, 0); // apres minuit : la plage a demarre hier
        var (remainingEarly, totalEarly) = Periods.GetTiming(p, earlyMorning);
        Assert.Equal(3600, remainingEarly); // 01h -> 02h = 1h restante
        Assert.Equal(14400, totalEarly);
    }

    [Fact]
    public void HasEnabledPeriod_ReflectsAtLeastOneEnabledPeriod_RegardlessOfActiveNow()
    {
        Assert.False(Periods.HasEnabledPeriod(new PeriodsData()));
        Assert.False(Periods.HasEnabledPeriod(new PeriodsData { Periods = new List<Period> { new() { Enabled = false } } }));
        Assert.True(Periods.HasEnabledPeriod(new PeriodsData { Periods = new List<Period> { new() { Enabled = false }, new() { Enabled = true } } }));
    }
}
