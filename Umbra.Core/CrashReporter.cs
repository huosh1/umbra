using System.Text;

namespace Umbra.Core;

public static class CrashReporter
{
    private const int MaxCrashLogs = 20;

    public static string LogDirectory => Config.LogsDir;

    public static void EnsureLogDirectory() => Directory.CreateDirectory(LogDirectory);

    public static string? Write(Exception exception, string source, string? appVersion = null)
    {
        ArgumentNullException.ThrowIfNull(exception);
        try
        {
            EnsureLogDirectory();
            var timestamp = DateTimeOffset.Now;
            var path = Path.Combine(
                LogDirectory,
                $"crash-{timestamp:yyyyMMdd-HHmmss-fff}-{Environment.ProcessId}-{Guid.NewGuid():N}.log");
            var contents = new StringBuilder()
                .AppendLine("Umbra crash report")
                .AppendLine($"Time: {timestamp:O}")
                .AppendLine($"Source: {source}")
                .AppendLine($"Version: {appVersion ?? "unknown"}")
                .AppendLine($"Process: {Environment.ProcessId}")
                .AppendLine($"OS: {Environment.OSVersion}")
                .AppendLine($"Runtime: {Environment.Version}")
                .AppendLine()
                .AppendLine(exception.ToString())
                .ToString();
            File.WriteAllText(path, contents, Encoding.UTF8);
            PruneOldLogs();
            return path;
        }
        catch
        {
            return null;
        }
    }

    private static void PruneOldLogs()
    {
        try
        {
            foreach (var file in new DirectoryInfo(LogDirectory)
                         .EnumerateFiles("crash-*.log")
                         .OrderByDescending(file => file.LastWriteTimeUtc)
                         .Skip(MaxCrashLogs))
            {
                file.Delete();
            }
        }
        catch
        {
        }
    }
}
