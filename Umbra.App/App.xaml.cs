using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using Umbra.Core;

namespace Umbra.App;

public partial class App : Application
{
    private CancellationTokenSource? _watchdogCts;
    private TrayIcon? _trayIcon;
    private MainWindow? _dashboard;
    private bool _reallyQuitting;
    private DispatcherTimer? _reminderTimer;
    private DateTime? _lastReminderDate;
    private readonly SemaphoreSlim _updateGate = new(1, 1);

    public string InstalledVersion { get; } = GetInstalledVersion();
    public UpdateUiStatus UpdateStatus { get; private set; } = new(UpdatePhase.Idle, GetInstalledVersion());
    public event Action? UpdateStatusChanged;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        if (e.Args.Contains("--watchdog"))
        {
            // Processus détaché, headless, aucune fenêtre : c'est lui, et
            // uniquement lui, qui applique les blocages (voir WatchdogLoop
            // dans Umbra.Core) - il survit à la fermeture du tableau de
            // bord. ShutdownMode=OnExplicitShutdown (voir App.xaml) garde
            // le process en vie sans fenêtre.
            _watchdogCts = new CancellationTokenSource();
            _ = WatchdogLoop.RunAsync(NotifyPlaceholder, _watchdogCts.Token);
            return;
        }

        // Applique le thème enregistré (Dark par défaut) - voir AppTheme.cs
        // pour pourquoi ça ne peut pas se limiter à ApplicationThemeManager.
        AppTheme.Apply(Settings.Load().Theme);

        // The executable path can change after an update. Refreshing the
        // per-user native messaging registration on every normal launch keeps
        // Chrome/Vivaldi/Edge/Brave connected without manual repair steps.
        BrowserIntegration.RegisterNativeHost();

        _dashboard = new MainWindow();
        // Fermer la fenêtre (croix) masque vers le systray plutôt que de
        // quitter - seul "Quitter" dans le menu de l'icône termine
        // vraiment le process, pour que le watchdog élevé reste supervisé
        // même quand le tableau de bord n'est pas affiché à l'écran.
        _dashboard.Closing += (_, args) =>
        {
            if (_reallyQuitting) return;
            args.Cancel = true;
            _dashboard.Hide();
        };
        _dashboard.Show();

        _trayIcon = new TrayIcon(_dashboard, "Umbra");
        _trayIcon.DoubleClicked += ShowDashboard;
        _trayIcon.SetMenu(new (string, Action?)[]
        {
            ("Ouvrir Umbra", ShowDashboard),
            ("-", null),
            ("Session rapide (25 min)", () => QuickStart(25)),
            ("Session rapide (60 min)", () => QuickStart(60)),
            ("-", null),
            ("Quitter", QuitReally),
        });

        _reminderTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(1) };
        _reminderTimer.Tick += (_, _) => CheckSmartReminder();
        _reminderTimer.Start();

        _ = CheckForUpdatesAfterStartupAsync();
    }

    private void ShowDashboard()
    {
        if (_dashboard == null) return;
        _dashboard.Show();
        _dashboard.WindowState = WindowState.Normal;
        _dashboard.Activate();
    }

    private void QuickStart(double minutes)
    {
        var s = Session.Load();
        if (!s.Active)
        {
            Session.StartCustom(minutes, hardMode: false, History.DefaultQuest);
            WatchdogSupervisor.Ensure();
        }
        ShowDashboard();
    }

    private void QuitReally()
    {
        _reallyQuitting = true;
        _reminderTimer?.Stop();
        _trayIcon?.Dispose();
        MusicHistory.Flush();
        Shutdown();
    }

    public async Task CheckForUpdatesAsync(bool userInitiated)
    {
        if (!await _updateGate.WaitAsync(0)) return;
        var watchdogStoppedForUpdate = false;
        try
        {
            SetUpdateStatus(new UpdateUiStatus(UpdatePhase.Checking, InstalledVersion));
            var update = await Updater.CheckForUpdateAsync(InstalledVersion);
            if (!update.CheckSucceeded)
            {
                SetUpdateStatus(new UpdateUiStatus(UpdatePhase.Failed, InstalledVersion));
                if (userInitiated) ShowUpdateMessage(Loc.T("update.check.failed"), MessageBoxImage.Warning);
                return;
            }

            if (!update.Available)
            {
                SetUpdateStatus(new UpdateUiStatus(UpdatePhase.UpToDate, InstalledVersion, update.LatestVersion));
                if (userInitiated) ShowUpdateMessage(Loc.T("update.current"), MessageBoxImage.Information);
                return;
            }

            SetUpdateStatus(new UpdateUiStatus(UpdatePhase.Available, InstalledVersion, update.LatestVersion));
            if (IsFocusActivityActive())
            {
                if (userInitiated) ShowUpdateMessage(Loc.T("update.activity.active"), MessageBoxImage.Information);
                else _trayIcon?.ShowNotification(Loc.T("update.available.title"),
                    string.Format(Loc.T("update.available.notification"), update.LatestVersion));
                return;
            }

            if (!userInitiated && (_dashboard is null || !_dashboard.IsVisible))
            {
                _trayIcon?.ShowNotification(Loc.T("update.available.title"),
                    string.Format(Loc.T("update.available.notification"), update.LatestVersion));
                return;
            }

            if (!update.CanInstall)
            {
                var openRelease = ShowUpdateQuestion(string.Format(Loc.T("update.manual"), update.LatestVersion));
                if (openRelease && !string.IsNullOrWhiteSpace(update.ReleaseUrl))
                    Process.Start(new ProcessStartInfo(update.ReleaseUrl) { UseShellExecute = true });
                return;
            }

            if (!ShowUpdateQuestion(string.Format(Loc.T("update.prompt"), update.LatestVersion))) return;

            var progress = new Progress<double>(value =>
                SetUpdateStatus(new UpdateUiStatus(UpdatePhase.Downloading, InstalledVersion, update.LatestVersion, value)));
            SetUpdateStatus(new UpdateUiStatus(UpdatePhase.Downloading, InstalledVersion, update.LatestVersion));
            var installerPath = await Updater.DownloadInstallerAsync(update, progress);

            if (IsFocusActivityActive())
            {
                SetUpdateStatus(new UpdateUiStatus(UpdatePhase.Available, InstalledVersion, update.LatestVersion));
                ShowUpdateMessage(Loc.T("update.activity.active"), MessageBoxImage.Information);
                return;
            }
            watchdogStoppedForUpdate = await WatchdogSupervisor.StopForUpdateAsync();
            if (!watchdogStoppedForUpdate)
                throw new InvalidOperationException("The Umbra watchdog did not stop for the update.");

            SetUpdateStatus(new UpdateUiStatus(UpdatePhase.Installing, InstalledVersion, update.LatestVersion, 1));

            var installer = Process.Start(new ProcessStartInfo
            {
                FileName = installerPath,
                Arguments = "/UPDATE=1 /VERYSILENT /SUPPRESSMSGBOXES /NORESTART /CLOSEAPPLICATIONS",
                UseShellExecute = true,
                WorkingDirectory = Path.GetDirectoryName(installerPath),
            });
            if (installer is null) throw new InvalidOperationException("Unable to start the Umbra installer.");
            QuitReally();
        }
        catch (Exception)
        {
            if (watchdogStoppedForUpdate && (Session.Load().Active || Periods.HasEnabledPeriod(Periods.Load())))
                WatchdogSupervisor.Ensure();
            SetUpdateStatus(new UpdateUiStatus(UpdatePhase.Failed, InstalledVersion));
            ShowUpdateMessage(Loc.T("update.install.failed"), MessageBoxImage.Error);
        }
        finally
        {
            _updateGate.Release();
        }
    }

    private async Task CheckForUpdatesAfterStartupAsync()
    {
        await Task.Delay(TimeSpan.FromSeconds(6));
        if (_reallyQuitting) return;
        await CheckForUpdatesAsync(userInitiated: false);
    }

    private void SetUpdateStatus(UpdateUiStatus status)
    {
        UpdateStatus = status;
        UpdateStatusChanged?.Invoke();
    }

    private bool ShowUpdateQuestion(string message)
    {
        var result = _dashboard is { IsVisible: true }
            ? MessageBox.Show(_dashboard, message, Loc.T("update.available.title"), MessageBoxButton.YesNo, MessageBoxImage.Information)
            : MessageBox.Show(message, Loc.T("update.available.title"), MessageBoxButton.YesNo, MessageBoxImage.Information);
        return result == MessageBoxResult.Yes;
    }

    private void ShowUpdateMessage(string message, MessageBoxImage image)
    {
        if (_dashboard is { IsVisible: true })
            MessageBox.Show(_dashboard, message, Loc.T("update.available.title"), MessageBoxButton.OK, image);
        else
            MessageBox.Show(message, Loc.T("update.available.title"), MessageBoxButton.OK, image);
    }

    private static string GetInstalledVersion()
    {
        var version = typeof(App).Assembly.GetName().Version;
        if (version is null) return "1.0.0";
        return $"{Math.Max(version.Major, 0)}.{Math.Max(version.Minor, 0)}.{Math.Max(version.Build, 0)}";
    }

    private static bool IsFocusActivityActive()
    {
        if (Session.Load().Active) return true;
        return Periods.GetActivePeriods(Periods.Load(), DateTime.Now).Count > 0;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        MusicHistory.Flush();
        base.OnExit(e);
    }

    private void CheckSmartReminder()
    {
        if (_trayIcon is null || Session.Load().Active) return;
        var settings = Settings.Load();
        if (settings.SmartReminderMode == "off") return;

        TimeSpan target;
        if (settings.SmartReminderMode == "automatic")
        {
            var hour = History.GetSuggestedStartHour();
            if (hour is null) return;
            target = TimeSpan.FromHours(hour.Value);
        }
        else if (!TimeSpan.TryParse(settings.SmartReminderTime, out target))
        {
            return;
        }

        var now = DateTime.Now;
        var elapsed = now.TimeOfDay - target;
        if (elapsed.TotalMinutes is < 0 or > 5 || _lastReminderDate == now.Date) return;
        _lastReminderDate = now.Date;
        _trayIcon.ShowNotification(Loc.T("reminder.notification.title"), Loc.T("reminder.notification.body"));
    }

    // Notifications toast natives à brancher plus tard - pour l'instant on
    // se contente de logger la transition, pas bloquant pour le reste du
    // portage.
    private static void NotifyPlaceholder(string key) => WatchdogLoop.Log($"notify: {key}");
}
