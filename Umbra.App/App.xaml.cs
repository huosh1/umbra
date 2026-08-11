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
