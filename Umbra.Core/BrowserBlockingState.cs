namespace Umbra.Core;

public record BrowserBlockState(bool Blocking, List<string> Sites);

public static class BrowserBlockingState
{
    public static BrowserBlockState GetCurrent(DateTime? nowOpt = null)
    {
        var now = nowOpt ?? DateTime.Now;
        var sessionBlocking = Session.IsBlockingActive(Session.Load());
        var activePeriods = Periods.GetActivePeriods(Periods.Load(), now);
        if (!sessionBlocking && activePeriods.Count == 0) return new BrowserBlockState(false, new());

        var sites = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (sessionBlocking)
        {
            foreach (var site in Blocklist.Load().Sites) AddSite(sites, site);
        }
        foreach (var period in activePeriods)
        {
            foreach (var site in period.Sites) AddSite(sites, site);
        }
        return new BrowserBlockState(true, sites.OrderBy(site => site, StringComparer.OrdinalIgnoreCase).ToList());
    }

    private static void AddSite(HashSet<string> sites, string? value)
    {
        var site = (value ?? "").Trim().ToLowerInvariant();
        if (site.StartsWith("www.")) site = site[4..];
        if (site.Length > 0) sites.Add(site);
    }
}
