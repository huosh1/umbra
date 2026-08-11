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
    private SingleInstanceCoordinator? _singleInstance;

    public string InstalledVersion { get; } = GetInstalledVersion();
    public UpdateUiStatus UpdateStatus { get; private set; } = new(UpdatePhase.Idle, GetInstalledVersion());
    public event Action? UpdateStatusChanged;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        RegisterCrashHandlers();

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

        _singleInstance = new SingleInstanceCoordinator("UmbraNative.Desktop");
        if (!_singleInstance.IsPrimary)
        {
            // The pinned taskbar shortcut starts the executable again after
            // the dashboard was hidden to the tray. Wake the existing
            // process, then end this short-lived secondary process.
            _singleInstance.SignalPrimary();
            Shutdown();
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

        _singleInstance.Listen(RequestDashboardActivation);

        _trayIcon = new TrayIcon(_dashboard, "Umbra");
        _trayIcon.DoubleClicked += ShowDashboard;
        _trayIcon.MenuOpening += RefreshTrayMenu;
        RefreshTrayMenu();

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
        // Activate() can be denied when another process initiated the request.
        // Briefly toggling Topmost reliably brings the existing WPF window
        // forward without leaving it permanently above other applications.
        _dashboard.Topmost = true;
        _dashboard.Topmost = false;
        _dashboard.Focus();
    }

    private void RequestDashboardActivation()
    {
        if (_reallyQuitting || Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished) return;
        Dispatcher.BeginInvoke(ShowDashboard);
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

    private void RefreshTrayMenu()
    {
        if (_trayIcon is null) return;
        var items = new List<(string, Action?)>
        {
            (Loc.T("tray.open"), ShowDashboard),
            ("-", null),
        };

        var session = Session.Load();
        var activeSchedule = session.Active
            ? null
            : Periods.GetActivePeriods(Periods.Load(), DateTime.Now).FirstOrDefault();
        if (session.Active)
        {
            var title = session.Kind == "pomodoro" && session.Pomodoro is { } pomodoro
                ? Loc.T(pomodoro.Phase == "break" ? "tray.pomodoro.break" : "tray.pomodoro.focus")
                : string.IsNullOrWhiteSpace(session.QuestName) ? Loc.T("tray.free") : session.QuestName;
            items.Add((string.Format(Loc.T("tray.status"), title, FormatTrayDuration(Session.RemainingSeconds(session))), null));
            items.Add((Loc.T("tray.floating"), FloatingFocusWindow.ShowOrActivate));
            items.Add(Session.CanStop(session)
                ? (Loc.T("tray.stop"), StopCurrentSessionFromTray)
                : (Loc.T("tray.stop.locked"), null));
            items.Add(("-", null));
        }
        else if (activeSchedule is not null)
        {
            var remaining = TimeSpan.FromMinutes(Periods.MinutesUntilEnd(activeSchedule, DateTime.Now));
            var title = string.Format(Loc.T("tray.schedule"), activeSchedule.Name);
            items.Add((string.Format(Loc.T("tray.status"), title, FormatTrayDuration(remaining.TotalSeconds)), null));
            items.Add((Loc.T("tray.floating"), FloatingFocusWindow.ShowOrActivate));
            items.Add(("-", null));
        }
        else
        {
            items.Add((string.Format(Loc.T("focus.quick"), 25), () => QuickStart(25)));
            items.Add((string.Format(Loc.T("focus.quick"), 60), () => QuickStart(60)));
            items.Add(("-", null));
        }

        items.Add((Loc.T("tray.quit"), QuitReally));
        _trayIcon.SetMenu(items);
    }

    private void StopCurrentSessionFromTray()
    {
        var session = Session.Load();
        if (!session.Active || !Session.CanStop(session)) return;
        Session.Stop(session);
        RefreshTrayMenu();
    }

    private static string FormatTrayDuration(double seconds)
    {
        var rounded = Math.Max(0, (int)Math.Ceiling(seconds));
        var duration = TimeSpan.FromSeconds(rounded);
        return duration.TotalHours >= 1
            ? $"{(int)duration.TotalHours}:{duration.Minutes:00}:{duration.Seconds:00}"
            : $"{duration.Minutes:00}:{duration.Seconds:00}";
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
        var browserHostsStoppedForUpdate = false;
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
            browserHostsStoppedForUpdate = await BrowserIntegration.StopNativeHostsForUpdateAsync();
            if (!browserHostsStoppedForUpdate)
                throw new InvalidOperationException("The Umbra browser host did not stop for the update.");

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
        catch (Exception error)
        {
            CrashReporter.Write(error, "update", InstalledVersion);
            if (browserHostsStoppedForUpdate)
                BrowserIntegration.ResumeNativeHostsAfterFailedUpdate();
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
        return UpdateReadiness.Evaluate(Session.Load(), Periods.Load(), DateTime.Now) != UpdateBlockReason.None;
    }

    private void RegisterCrashHandlers()
    {
        DispatcherUnhandledException += (_, args) =>
            CrashReporter.Write(args.Exception, "dispatcher", InstalledVersion);
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            CrashReporter.Write(
                args.ExceptionObject as Exception ?? new Exception(args.ExceptionObject?.ToString() ?? "Unknown fatal error"),
                "app-domain",
                InstalledVersion);
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            CrashReporter.Write(args.Exception, "unobserved-task", InstalledVersion);
            args.SetObserved();
        };
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _singleInstance?.Dispose();
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
