namespace Umbra.Core;

public static class BrowserHostLifecycle
{
    public static bool IsStopRequested() => File.Exists(Config.BrowserHostStopRequestFile);

    public static bool RequestStop()
    {
        try
        {
            File.WriteAllText(Config.BrowserHostStopRequestFile, DateTime.UtcNow.ToString("O"));
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static void ClearStopRequest()
    {
        try
        {
            if (File.Exists(Config.BrowserHostStopRequestFile))
                File.Delete(Config.BrowserHostStopRequestFile);
        }
        catch
        {
        }
    }
}
