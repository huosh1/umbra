using Umbra.Core;

namespace Umbra.Tests;

public class SettingsTests : IDisposable
{
    private readonly string _tempDir;

    public SettingsTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "umbra-settings-tests-" + Guid.NewGuid());
        Config.DataDir = _tempDir;
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { }
    }

    [Fact]
    public void SmartReminderSettings_RoundTrip()
    {
        Settings.Save(new AppSettings { SmartReminderMode = "manual", SmartReminderTime = "14:30" });
        var loaded = Settings.Load();
        Assert.Equal("manual", loaded.SmartReminderMode);
        Assert.Equal("14:30", loaded.SmartReminderTime);
    }
}
