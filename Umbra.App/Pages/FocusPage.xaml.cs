using System.Media;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using Umbra.Core;

namespace Umbra.App.Pages;

public partial class FocusPage : System.Windows.Controls.UserControl
{
    private readonly DispatcherTimer _timer;
    private bool _loaded;
    private List<SavedBlocklist> _savedBlocklists = new();
    private readonly string _clockStyle;

    // Suivi des transitions pour déclencher les sons de fin de session/pause
    // (Réglages) - même logique de garde que WatchdogLoop.Enforcer côté
    // Core : jamais de son au tout premier tick (une session déjà en cours
    // au chargement de la page ne doit pas sonner immédiatement).
    private bool _soundStateInitialized;
    private bool _lastActive;
    private string? _lastKind;
    private string? _lastPhase;

    public FocusPage()
    {
        InitializeComponent();

        QuestNameBox.PlaceholderText = Loc.T("focus.quest.placeholder");
        BlocklistLabel.Text = Loc.T("focus.blocklist.label");
        FocusSliderLabel.Text = Loc.T("focus.slider.focus");
        RestSliderLabel.Text = Loc.T("focus.slider.rest");
        RepeatsSliderLabel.Text = Loc.T("focus.slider.repeats");
        HardModeLabel.Text = Loc.T("focus.hardmode");
        EndStatLabel.Text = Loc.T("focus.stat.end");
        DurationStatLabel.Text = Loc.T("focus.stat.duration");
        FocusStatLabel.Text = Loc.T("focus.stat.focus");
        RestStatLabel.Text = Loc.T("focus.stat.rest");
        PomodoroTab.Header = Loc.T("focus.mode.pomodoro");
        FreeTab.Header = Loc.T("focus.mode.free");
        SchedulesTab.Header = Loc.T("focus.mode.schedules");
        FreeDurationLabel.Text = Loc.T("focus.free.duration");
        StopButton.Content = Loc.T("focus.stop");
        StartButton.Content = Loc.T("focus.start");
        PopoutButton.ToolTip = Loc.T("focus.popout");

        BuildBlocklistCombo();

        var settings = Settings.Load();
        _clockStyle = settings.FocusClockStyle;
        FocusSlider.Value = settings.DurationPresets.Count > 0 ? settings.DurationPresets[0] : 25;
        RestSlider.Value = 5;
        RepeatsSlider.Value = 2;
        FreeDurationSlider.Value = settings.DurationPresets.Count > 0 ? settings.DurationPresets[0] : 25;

        _loaded = true;
        RefreshSessionUi();

        if (Periods.HasEnabledPeriod(Periods.Load()))
        {
            WatchdogSupervisor.Ensure();
        }

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (_, _) => RefreshSessionUi();
        _timer.Start();
        Unloaded += (_, _) => _timer.Stop();
    }

    // "Liste actuelle" (index 0) laisse le blocklist.json actif tel quel -
    // choisir une liste enregistrée la copie dedans au démarrage de la
    // session (voir StartButton_Click), sans toucher au watchdog qui lit
    // déjà toujours cette liste "active" pour le blocage lié aux sessions.
    private void BuildBlocklistCombo()
    {
        _savedBlocklists = SavedBlocklists.Load();
        BlocklistCombo.Items.Clear();
        BlocklistCombo.Items.Add(Loc.T("focus.blocklist.current"));
        foreach (var list in _savedBlocklists) BlocklistCombo.Items.Add(list.Name);
        BlocklistCombo.SelectedIndex = 0;
    }

    private void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_loaded) UpdatePreview();
    }

    private void FreeDurationSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (FreeDurationValue is not null) FreeDurationValue.Text = $"{(int)e.NewValue} min";
    }

    private void FocusModeTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (StartButton is null || FocusModeTabs is null) return;
        StartButton.Visibility = FocusModeTabs.SelectedItem == SchedulesTab ? Visibility.Collapsed : Visibility.Visible;
        HardModePanel.Visibility = FocusModeTabs.SelectedItem == SchedulesTab ? Visibility.Collapsed : Visibility.Visible;
    }

    // Aperçu "si je démarre maintenant" affiché tant qu'aucune session n'est
    // active - Répétitions=0 => session brute (Focus seul, pas de repos, pas
    // de cycles) ; Répétitions>=1 => vrai pomodoro. Les totaux reflètent
    // exactement le calendrier réel de Session.StartPomodoro (le dernier
    // cycle de travail ne termine pas sur un repos - voir Session.cs).
    private void UpdatePreview()
    {
        var focusMin = (int)FocusSlider.Value;
        var restMin = (int)RestSlider.Value;
        var repeats = (int)RepeatsSlider.Value;

        FocusSliderValue.Text = focusMin.ToString();
        RestSliderValue.Text = restMin.ToString();
        RepeatsSliderValue.Text = repeats.ToString();
        RestSlider.IsEnabled = repeats > 0;

        int totalFocus, totalRest;
        if (repeats <= 0)
        {
            totalFocus = focusMin;
            totalRest = 0;
        }
        else
        {
            totalFocus = repeats * focusMin;
            totalRest = Math.Max(0, repeats - 1) * restMin;
        }
        var totalDuration = totalFocus + totalRest;

        EndStatValue.Text = DateTime.Now.AddMinutes(totalDuration).ToString("HH:mm");
        DurationStatValue.Text = FormatHm(totalDuration);
        FocusStatValue.Text = FormatHm(totalFocus);
        RestStatValue.Text = FormatHm(totalRest);
    }

    private static string FormatHm(int minutes) => $"{minutes / 60:D2}:{minutes % 60:D2}";

    private void RefreshSessionUi()
    {
        var s = Session.Load(); // fait aussi avancer les phases pomodoro dues, termine une session custom expirée côté watchdog
        var active = s.Active;

        ActivePanel.Visibility = active ? Visibility.Visible : Visibility.Collapsed;
        ConfigPanel.Visibility = active ? Visibility.Collapsed : Visibility.Visible;

        if (active)
        {
            var remaining = TimeSpan.FromSeconds(Session.RemainingSeconds(s));
            SessionStatusText.Text = s.HardMode ? Loc.T("focus.status.active.hard") : Loc.T("focus.status.active");
            SessionTimeText.Text = $"{(int)remaining.TotalMinutes:D2}:{remaining.Seconds:D2}";
            var canStop = Session.CanStop(s);
            StopButton.Content = canStop ? Loc.T("focus.stop") : Loc.T("focus.locked");
            StopButton.IsEnabled = canStop;
            var totalSeconds = Math.Max(1, (s.EndTs - s.StartTs) / 1000d);
            ActiveRingHost.Children.Clear();
            ActiveRingHost.Children.Add(RingVisual.BuildFocusTimer(245, Session.RemainingSeconds(s) / totalSeconds,
                (System.Windows.Media.Brush)FindResource("ControlStrokeColorDefaultBrush"),
                (System.Windows.Media.Brush)FindResource("AccentFillColorDefaultBrush"), SessionTimeText.Text, Foreground, _clockStyle));
        }
        else
        {
            UpdatePreview();
        }

        WatchdogStatusText.Text = WatchdogSupervisor.IsAlive() ? Loc.T("focus.watchdog.active") : Loc.T("focus.watchdog.inactive");


        CheckSoundTransitions(s);
    }

    // "Fin de période de focus" = une session custom se termine, ou une
    // phase pomodoro "travail" se termine (pause suivante ou fin de
    // session) ; "fin de pause" = la phase "pause" se termine (retour au
    // travail).
    private void CheckSoundTransitions(SessionState s)
    {
        var curPhase = s.Kind == "pomodoro" && s.Pomodoro != null ? s.Pomodoro.Phase : null;

        if (_soundStateInitialized)
        {
            var focusPeriodEnded =
                (_lastActive && !s.Active && _lastKind != "pomodoro") ||
                (_lastActive && _lastKind == "pomodoro" && _lastPhase == "work" && (curPhase == "break" || !s.Active));
            var breakEnded = _lastActive && _lastKind == "pomodoro" && _lastPhase == "break" && curPhase == "work";

            var settings = Settings.Load();
            if (focusPeriodEnded && settings.PlayEndOfSessionSound) SystemSounds.Exclamation.Play();
            if (breakEnded && settings.PlayEndOfBreakSound) SystemSounds.Asterisk.Play();
        }

        _soundStateInitialized = true;
        _lastActive = s.Active;
        _lastKind = s.Kind;
        _lastPhase = curPhase;
    }

    private void StartButton_Click(object sender, RoutedEventArgs e)
    {
        if (FocusModeTabs.SelectedItem == FreeTab)
        {
            StartFreeSession((int)FreeDurationSlider.Value);
            return;
        }
        var focusMin = (int)FocusSlider.Value;
        var restMin = (int)RestSlider.Value;
        var repeats = (int)RepeatsSlider.Value;
        var hardMode = HardModeToggle.IsChecked == true;
        var questName = string.IsNullOrWhiteSpace(QuestNameBox.Text) ? Loc.T("focus.quest.default") : QuestNameBox.Text.Trim();

        if (BlocklistCombo.SelectedIndex > 0)
        {
            var chosen = _savedBlocklists[BlocklistCombo.SelectedIndex - 1];
            Blocklist.Save(new BlocklistData { Apps = new List<string>(chosen.Apps), Sites = new List<string>(chosen.Sites) });
        }

        if (repeats <= 0)
        {
            Session.StartCustom(focusMin, hardMode, questName);
        }
        else
        {
            Session.StartPomodoro(focusMin, restMin, repeats, hardMode, questName);
        }
        WatchdogSupervisor.Ensure();
        RefreshSessionUi();
    }

    private void StartFreeSession(int minutes)
    {
        var hardMode = HardModeToggle.IsChecked == true;
        var questName = string.IsNullOrWhiteSpace(QuestNameBox.Text) ? Loc.T("focus.quest.default") : QuestNameBox.Text.Trim();
        ApplySelectedBlocklist();
        Session.StartCustom(minutes, hardMode, questName);
        WatchdogSupervisor.Ensure();
        RefreshSessionUi();
    }

    private void ApplySelectedBlocklist()
    {
        if (BlocklistCombo.SelectedIndex <= 0) return;
        var chosen = _savedBlocklists[BlocklistCombo.SelectedIndex - 1];
        Blocklist.Save(new BlocklistData { Apps = new List<string>(chosen.Apps), Sites = new List<string>(chosen.Sites) });
    }

    private void StopButton_Click(object sender, RoutedEventArgs e)
    {
        var s = Session.Load();
        if (!Session.CanStop(s)) return;
        Session.Stop(s);
        RefreshSessionUi();
    }

    private void PopoutButton_Click(object sender, RoutedEventArgs e) => FloatingFocusWindow.ShowOrActivate();
}
