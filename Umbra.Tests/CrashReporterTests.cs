using Umbra.Core;

namespace Umbra.Tests;

public sealed class CrashReporterTests : IDisposable
{
    private readonly string _tempDir;

    public CrashReporterTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "umbra-crash-reporter-tests-" + Guid.NewGuid());
        Config.DataDir = _tempDir;
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { }
    }

    [Fact]
    public void WriteCreatesUsefulLocalReport()
    {
        var path = CrashReporter.Write(new InvalidOperationException("test failure"), "unit-test", "1.0.4");

        Assert.NotNull(path);
        Assert.True(File.Exists(path));
        var contents = File.ReadAllText(path);
        Assert.Contains("Source: unit-test", contents);
        Assert.Contains("Version: 1.0.4", contents);
        Assert.Contains("InvalidOperationException", contents);
        Assert.Contains("test failure", contents);
    }

    [Fact]
    public void WriteRetainsOnlyTheTwentyMostRecentReports()
    {
        for (var i = 0; i < 24; i++)
            Assert.NotNull(CrashReporter.Write(new Exception($"failure {i}"), "retention-test"));

        Assert.Equal(20, Directory.GetFiles(CrashReporter.LogDirectory, "crash-*.log").Length);
    }
}
