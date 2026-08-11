namespace Umbra.App;

public enum UpdatePhase
{
    Idle,
    Checking,
    UpToDate,
    Available,
    Downloading,
    Installing,
    Failed,
}

public sealed record UpdateUiStatus(
    UpdatePhase Phase,
    string CurrentVersion,
    string? LatestVersion = null,
    double Progress = 0);
