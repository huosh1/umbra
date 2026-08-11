using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using Umbra.Core;

namespace Umbra.App.Pages;

public partial class SettingsPage : UserControl
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunValueName = "Umbra";

    private AppSettings _settings = Settings.Load();
    private bool _loaded;

    public SettingsPage()
    {
        InitializeComponent();
        _loaded = false;
        Loaded += SettingsPage_Loaded;
        Unloaded += SettingsPage_Unloaded;

        FocusSessionsHeaderText.Text = Loc.T("settings.section.focus_sessions");
        PeriodsTitleText.Text = Loc.T("settings.periods.title");
        PeriodsDescText.Text = Loc.T("settings.periods.desc");
        ClockStyleTitleText.Text = Loc.T("settings.clock.title");
        ClockStyleDescText.Text = Loc.T("settings.clock.desc");
        AddPresetButton.Content = Loc.T("settings.presets.add");
        SoundSessionTitleText.Text = Loc.T("settings.sound.session.title");
        SoundSessionDescText.Text = Loc.T("settings.sound.session.desc");
        SoundBreakTitleText.Text = Loc.T("settings.sound.break.title");
        SoundBreakDescText.Text = Loc.T("settings.sound.break.desc");
        SpotifyTitleText.Text = Loc.T("settings.spotify.title");
        SpotifyDescText.Text = Loc.T("settings.spotify.desc");
        SmartReminderTitleText.Text = Loc.T("settings.reminder.title");
        SmartReminderDescText.Text = Loc.T("settings.reminder.desc");
        SmartReminderOffItem.Content = Loc.T("settings.reminder.off");
        SmartReminderManualItem.Content = Loc.T("settings.reminder.manual");
        SmartReminderAutomaticItem.Content = Loc.T("settings.reminder.automatic");
        SmartReminderTimeLabelText.Text = Loc.T("settings.reminder.time");
        FloatingFocusTitleText.Text = Loc.T("settings.floating.title");
        FloatingFocusDescText.Text = Loc.T("settings.floating.desc");
        FloatingFocusBrowseButton.Content = Loc.T("settings.floating.choose");
        FloatingFocusClearButton.Content = Loc.T("settings.background.clear");
        FloatingFocusBlurLabel.Text = Loc.T("settings.floating.blur");
        FloatingFocusPresetsLabel.Text = Loc.T("settings.floating.presets");
        FloatingFocusRecentLabel.Text = Loc.T("settings.background.recent");

        GeneralHeaderText.Text = Loc.T("settings.section.general");
        LanguageLabelText.Text = Loc.T("settings.language.label");
        ThemeTitleText.Text = Loc.T("settings.theme.title");
        ThemeDescText.Text = Loc.T("settings.theme.desc");
        ThemeDarkItem.Content = Loc.T("settings.theme.dark");
        ThemeLightItem.Content = Loc.T("settings.theme.light");
        BackgroundTitleText.Text = Loc.T("settings.background.title");
        DefaultBackgroundsLabel.Text = Loc.T("settings.background.defaults");
        RecentBackgroundsLabel.Text = Loc.T("settings.background.recent");
        BrowseBackgroundButton.Content = Loc.T("settings.background.browse");
        ClearBackgroundButton.Content = Loc.T("settings.background.clear");
        BackgroundOpacityLabelText.Text = Loc.T("settings.background.opacity");
        BackgroundModeLabelText.Text = Loc.T("settings.background.mode");
        BackgroundModeDescText.Text = Loc.T("settings.background.mode.desc");
        BackgroundModeFullItem.Content = Loc.T("settings.background.mode.full");
        BackgroundModeContentItem.Content = Loc.T("settings.background.mode.content");
        BackgroundModeNavigationItem.Content = Loc.T("settings.background.mode.navigation");
        BackgroundBlurLabelText.Text = Loc.T("settings.background.blur");
        StartupLabelText.Text = Loc.T("settings.startup.label");
        UpdateTitleText.Text = Loc.T("settings.update.title");
        NotificationsTitleText.Text = Loc.T("settings.notifications.title");
        NotificationsDescText.Text = Loc.T("settings.notifications.desc");
        NotificationsLinkButton.ToolTip = Loc.T("settings.notifications.link");
        BrowserExtensionTitleText.Text = Loc.T("settings.extension.title");
        BrowserExtensionStatusText.Text = Loc.T("settings.extension.desc");
        BrowserExtensionInstallButton.Content = Loc.T("settings.extension.install");
        PrivacyTitleText.Text = Loc.T("settings.privacy.title");
        PrivacyDescText.Text = Loc.T("settings.privacy.desc");
        ClearHistoryButton.Content = Loc.T("settings.privacy.clear");

        LanguageBox.SelectedIndex = _settings.Language == "en" ? 1 : 0;
        ThemeBox.SelectedIndex = _settings.Theme == "Light" ? 1 : 0;
        SoundSessionToggle.IsChecked = _settings.PlayEndOfSessionSound;
        SoundBreakToggle.IsChecked = _settings.PlayEndOfBreakSound;
        SpotifyToggle.IsChecked = _settings.ShowSpotifyTile;
        SmartReminderModeBox.SelectedIndex = _settings.SmartReminderMode switch { "manual" => 1, "automatic" => 2, _ => 0 };
        SmartReminderTimeBox.Text = _settings.SmartReminderTime;
        FloatingFocusBlurSlider.Value = _settings.FloatingFocusBlur;
        RenderFloatingFocusStatus();
        UpdateSmartReminderUi();
        BackgroundOpacitySlider.Value = _settings.BackgroundOverlayOpacity;
        BackgroundBlurSlider.Value = _settings.BackgroundBlur;
        BackgroundModeBox.SelectedIndex = _settings.BackgroundAppearanceMode switch { "content" => 1, "navigation" => 2, _ => 0 };
        RenderPresets();
        RenderClockStyles();
        RenderBackgroundStatus();
        RefreshStartupStatus();
        RefreshUpdateStatus();
        _loaded = true;
    }

    public void ApplySearch(string query)
    {
        var normalizedQuery = NormalizeSearchText(query);
        foreach (UIElement child in SettingsRootPanel.Children)
        {
            if (normalizedQuery.Length == 0) { child.Visibility = Visibility.Visible; continue; }
            if (child is TextBlock) { child.Visibility = Visibility.Collapsed; continue; }
            child.Visibility = NormalizeSearchText(CollectSearchText(child)).Contains(normalizedQuery, StringComparison.Ordinal)
                ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private static string CollectSearchText(DependencyObject element)
    {
        var parts = new List<string>();
        if (element is FrameworkElement { Tag: not null } tagged) parts.Add(tagged.Tag.ToString() ?? "");
        if (element is TextBlock text) parts.Add(text.Text ?? "");
        if (element is ContentControl { Content: string content }) parts.Add(content);
        foreach (var child in LogicalTreeHelper.GetChildren(element).OfType<DependencyObject>()) parts.Add(CollectSearchText(child));
        return string.Join(' ', parts);
    }

    private static string NormalizeSearchText(string value)
    {
        var decomposed = value.ToLowerInvariant().Normalize(NormalizationForm.FormD);
        return new string(decomposed.Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark).ToArray());
    }

    private void RenderPresets()
    {
        PresetsPanel.Children.Clear();
        foreach (var minutes in _settings.DurationPresets)
        {
            var removeBtn = new Wpf.Ui.Controls.Button
            {
                Content = $"{minutes} min ✕",
                Appearance = Wpf.Ui.Controls.ControlAppearance.Secondary,
                Margin = new Thickness(0, 0, 8, 0),
            };
            removeBtn.Click += (_, _) => RemovePreset(minutes);
            PresetsPanel.Children.Add(removeBtn);
        }
    }

    private void RemovePreset(int minutes)
    {
        if (_settings.DurationPresets.Count <= 1) return;
        _settings.DurationPresets.Remove(minutes);
        Settings.Save(_settings);
        RenderPresets();
    }

    private void AddPreset_Click(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(NewPresetBox.Text.Trim(), out var minutes) || minutes <= 0) return;
        if (!_settings.DurationPresets.Contains(minutes))
        {
            _settings.DurationPresets.Add(minutes);
            _settings.DurationPresets.Sort();
            Settings.Save(_settings);
            RenderPresets();
        }
        NewPresetBox.Text = "";
    }

    private void LanguageBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_loaded) return;
        if (LanguageBox.SelectedItem is ComboBoxItem item && item.Tag is string lang)
        {
            _settings.Language = lang;
            Settings.Save(_settings);
            Loc.Language = lang;
        }
    }

    private void ThemeBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_loaded) return;
        if (ThemeBox.SelectedItem is ComboBoxItem item && item.Tag is string theme)
        {
            _settings.Theme = theme;
            Settings.Save(_settings);
            AppTheme.Apply(theme);
        }
    }

    private void SoundSessionToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (!_loaded) return;
        _settings.PlayEndOfSessionSound = SoundSessionToggle.IsChecked == true;
        Settings.Save(_settings);
    }

    private void SoundBreakToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (!_loaded) return;
        _settings.PlayEndOfBreakSound = SoundBreakToggle.IsChecked == true;
        Settings.Save(_settings);
    }

    private void SpotifyToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (!_loaded) return;
        _settings.ShowSpotifyTile = SpotifyToggle.IsChecked == true;
        Settings.Save(_settings);
    }

    private void SmartReminderModeBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SmartReminderModeBox.SelectedItem is not ComboBoxItem { Tag: string mode }) return;
        _settings.SmartReminderMode = mode;
        UpdateSmartReminderUi();
        if (_loaded) Settings.Save(_settings);
    }

    private void UpdateSmartReminderUi()
    {
        if (SmartReminderTimePanel is null || SmartReminderAutomaticHintText is null) return;
        SmartReminderTimePanel.Visibility = _settings.SmartReminderMode == "manual" ? Visibility.Visible : Visibility.Collapsed;
        SmartReminderAutomaticHintText.Visibility = _settings.SmartReminderMode == "automatic" ? Visibility.Visible : Visibility.Collapsed;
        var suggestedHour = History.GetSuggestedStartHour();
        SmartReminderAutomaticHintText.Text = suggestedHour is null
            ? Loc.T("settings.reminder.automatic.empty")
            : string.Format(Loc.T("settings.reminder.automatic.hint"), $"{suggestedHour:00}:00");
    }

    private void SmartReminderTimeBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (!TimeSpan.TryParse(SmartReminderTimeBox.Text.Trim(), out var value) || value.TotalHours is < 0 or >= 24)
        {
            SmartReminderTimeBox.Text = _settings.SmartReminderTime;
            return;
        }
        _settings.SmartReminderTime = $"{value.Hours:00}:{value.Minutes:00}";
        SmartReminderTimeBox.Text = _settings.SmartReminderTime;
        if (_loaded) Settings.Save(_settings);
    }

    private const int MaxRecentBackgrounds = 6;

    private void RenderClockStyles()
    {
        ClockStylePanel.Children.Clear();
        ClockStylePanel.Children.Add(CreateClockStyleButton("halo", Loc.T("settings.clock.halo")));
        ClockStylePanel.Children.Add(CreateClockStyleButton("orbit", Loc.T("settings.clock.orbit")));
        ClockStylePanel.Children.Add(CreateClockStyleButton("simple", Loc.T("settings.clock.simple")));
        ClockStylePanel.Children.Add(CreateClockStyleButton("arc", Loc.T("settings.clock.arc")));
        ClockStylePanel.Children.Add(CreateClockStyleButton("digital", Loc.T("settings.clock.digital")));
    }

    private Wpf.Ui.Controls.Button CreateClockStyleButton(string style, string label)
    {
        var selected = string.Equals(_settings.FocusClockStyle, style, StringComparison.OrdinalIgnoreCase);
        var accent = (Brush)FindResource("AccentFillColorDefaultBrush");
        var track = (Brush)FindResource("ControlStrokeColorDefaultBrush");
        var text = (Brush)FindResource("TextFillColorPrimaryBrush");

        var content = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
        content.Children.Add(RingVisual.BuildFocusTimer(108, 0.68, track, accent, "18:42", text, style));
        content.Children.Add(new TextBlock
        {
            Text = label,
            FontWeight = FontWeights.SemiBold,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 7, 0, 0),
            Foreground = text,
        });

        var button = new Wpf.Ui.Controls.Button
        {
            Tag = style,
            Content = content,
            Width = 190,
            Height = 154,
            Padding = new Thickness(12, 8, 12, 8),
            Margin = new Thickness(0, 0, 10, 8),
            Appearance = Wpf.Ui.Controls.ControlAppearance.Secondary,
            BorderBrush = selected ? accent : track,
            BorderThickness = selected ? new Thickness(2) : new Thickness(1),
            ToolTip = label,
        };
        button.Click += ClockStyleButton_Click;
        return button;
    }

    private void ClockStyleButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: string style }) return;
        _settings.FocusClockStyle = style;
        Settings.Save(_settings);
        RenderClockStyles();
    }

    private void RenderBackgroundStatus()
    {
        BackgroundCurrentText.Text = string.IsNullOrWhiteSpace(_settings.BackgroundImagePath)
            ? Loc.T("settings.background.none")
            : string.Format(Loc.T("settings.background.current"), System.IO.Path.GetFileName(_settings.BackgroundImagePath));
        RenderRecentBackgrounds();
        RenderDefaultBackgrounds();
    }

    private void RenderDefaultBackgrounds()
    {
        DefaultBackgroundsList.Items.Clear();
        foreach (var fileName in new[] { "water.png", "sand.png", "rosyhill.png", "gradient.png", "blisslike.png" })
        {
            var path = System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "Backgrounds", fileName);
            if (!System.IO.File.Exists(path)) continue;
            DefaultBackgroundsList.Items.Add(CreateBackgroundThumbnail(path));
        }
    }

    // Vignettes cliquables des images récemment utilisées (Réglages >
    // Background image), à côté de Parcourir/Retirer - permet de repasser
    // sur une ancienne image sans rouvrir l'explorateur de fichiers.
    private void RenderRecentBackgrounds()
    {
        RecentBackgroundsList.Items.Clear();
        foreach (var path in _settings.RecentBackgroundImages)
        {
            if (!System.IO.File.Exists(path)) continue;

            RecentBackgroundsList.Items.Add(CreateBackgroundThumbnail(path));
        }
        RecentBackgroundsLabel.Visibility = RecentBackgroundsList.Items.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        RecentBackgroundsList.Visibility = RecentBackgroundsList.Items.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private Border CreateBackgroundThumbnail(string path)
    {
            var isActive = string.Equals(path, _settings.BackgroundImagePath, StringComparison.OrdinalIgnoreCase);
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.DecodePixelWidth = 96;
            bitmap.UriSource = new Uri(path, UriKind.Absolute);
            bitmap.EndInit();
            bitmap.Freeze();

            var image = new System.Windows.Controls.Image
            {
                Source = bitmap,
                Width = 72,
                Height = 48,
                Stretch = System.Windows.Media.Stretch.UniformToFill,
            };
            var border = new System.Windows.Controls.Border
            {
                Width = 72,
                Height = 48,
                CornerRadius = new CornerRadius(6),
                Margin = new Thickness(0, 0, 8, 8),
                BorderThickness = new Thickness(isActive ? 2 : 0),
                BorderBrush = System.Windows.Media.Brushes.DodgerBlue,
                ClipToBounds = true,
                Cursor = System.Windows.Input.Cursors.Hand,
                ToolTip = System.IO.Path.GetFileName(path),
                Child = image,
            };
            border.MouseLeftButtonUp += (_, _) => SelectBackground(path);
            return border;
    }

    private void RememberRecentBackground(string path)
    {
        _settings.RecentBackgroundImages.RemoveAll(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase));
        _settings.RecentBackgroundImages.Insert(0, path);
        while (_settings.RecentBackgroundImages.Count > MaxRecentBackgrounds)
        {
            _settings.RecentBackgroundImages.RemoveAt(_settings.RecentBackgroundImages.Count - 1);
        }
    }

    private void SelectBackground(string path)
    {
        _settings.BackgroundImagePath = path;
        RememberRecentBackground(path);
        Settings.Save(_settings);
        RenderBackgroundStatus();
        (Window.GetWindow(this) as MainWindow)?.ApplyBackgroundImage(path);
    }

    private void BrowseBackground_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Images (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp",
        };
        if (dialog.ShowDialog() != true) return;

        SelectBackground(dialog.FileName);
    }

    private void ClearBackground_Click(object sender, RoutedEventArgs e)
    {
        _settings.BackgroundImagePath = null;
        Settings.Save(_settings);
        RenderBackgroundStatus();
        (Window.GetWindow(this) as MainWindow)?.ApplyBackgroundImage(null);
    }

    private void BackgroundOpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_loaded) return;
        _settings.BackgroundOverlayOpacity = e.NewValue;
        Settings.Save(_settings);
        (Window.GetWindow(this) as MainWindow)?.SetBackgroundOpacity(e.NewValue);
    }

    private void BackgroundModeBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_loaded || BackgroundModeBox.SelectedItem is not ComboBoxItem { Tag: string mode }) return;
        _settings.BackgroundAppearanceMode = mode;
        Settings.Save(_settings);
        (Window.GetWindow(this) as MainWindow)?.RefreshBackgroundAppearance();
    }

    private void BackgroundBlurSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_loaded) return;
        _settings.BackgroundBlur = e.NewValue;
        Settings.Save(_settings);
        (Window.GetWindow(this) as MainWindow)?.RefreshBackgroundAppearance();
    }

    private void RenderFloatingFocusStatus()
    {
        FloatingFocusCurrentText.Text = string.IsNullOrWhiteSpace(_settings.FloatingFocusBackgroundPath)
            ? Loc.T("settings.background.none")
            : string.Format(Loc.T("settings.background.current"), System.IO.Path.GetFileName(_settings.FloatingFocusBackgroundPath));
        FloatingFocusPresetsList.Items.Clear();
        var presetFolder = System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "FloatingBackgrounds");
        if (Directory.Exists(presetFolder))
            foreach (var path in Directory.EnumerateFiles(presetFolder).Where(IsSupportedFloatingBackground))
                FloatingFocusPresetsList.Items.Add(CreateFloatingBackgroundPreview(path));
        FloatingFocusRecentList.Items.Clear();
        foreach (var path in _settings.RecentFloatingFocusBackgrounds.Where(File.Exists))
            FloatingFocusRecentList.Items.Add(CreateFloatingBackgroundPreview(path));
        FloatingFocusRecentLabel.Visibility = FloatingFocusRecentList.Items.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        FloatingFocusRecentList.Visibility = FloatingFocusRecentList.Items.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private static bool IsSupportedFloatingBackground(string path) =>
        new[] { ".png", ".jpg", ".jpeg", ".bmp", ".mp4" }.Contains(System.IO.Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);

    private Border CreateFloatingBackgroundPreview(string path)
    {
        FrameworkElement preview;
        var isVideo = System.IO.Path.GetExtension(path).Equals(".mp4", StringComparison.OrdinalIgnoreCase);
        Grid? videoHost = null;
        if (isVideo)
        {
            videoHost = new Grid { Background = new SolidColorBrush(Color.FromRgb(28, 30, 36)) };
            videoHost.Children.Add(new TextBlock { Text = "▶  MP4", FontSize = 12, Opacity = 0.7, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center });
            preview = videoHost;
        }
        else
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit(); bitmap.CacheOption = BitmapCacheOption.OnLoad; bitmap.DecodePixelWidth = 160; bitmap.UriSource = new Uri(path); bitmap.EndInit(); bitmap.Freeze();
            preview = new Image { Source = bitmap, Stretch = Stretch.UniformToFill };
        }
        var border = new Border
        {
            Width = 112, Height = 68, Margin = new Thickness(0, 0, 8, 8), CornerRadius = new CornerRadius(7), ClipToBounds = true,
            BorderThickness = new Thickness(string.Equals(path, _settings.FloatingFocusBackgroundPath, StringComparison.OrdinalIgnoreCase) ? 2 : 0),
            BorderBrush = Brushes.DodgerBlue, Cursor = System.Windows.Input.Cursors.Hand, ToolTip = System.IO.Path.GetFileNameWithoutExtension(path), Child = preview
        };
        border.MouseLeftButtonUp += (_, _) => SelectFloatingBackground(path, remember: false);
        if (isVideo && videoHost is not null)
        {
            MediaElement? activeVideo = null;
            border.MouseEnter += (_, _) =>
            {
                if (activeVideo is not null) return;
                activeVideo = new MediaElement { Source = new Uri(path), LoadedBehavior = MediaState.Manual, UnloadedBehavior = MediaState.Stop, IsMuted = true, Stretch = Stretch.UniformToFill };
                activeVideo.MediaEnded += (_, _) => { activeVideo.Position = TimeSpan.Zero; activeVideo.Play(); };
                videoHost.Children.Add(activeVideo);
                activeVideo.Play();
            };
            border.MouseLeave += (_, _) =>
            {
                if (activeVideo is null) return;
                activeVideo.Stop();
                videoHost.Children.Remove(activeVideo);
                activeVideo.Source = null;
                activeVideo = null;
            };
        }
        return border;
    }

    private void SelectFloatingBackground(string path, bool remember)
    {
        _settings.FloatingFocusBackgroundPath = path;
        if (remember)
        {
            _settings.RecentFloatingFocusBackgrounds.RemoveAll(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase));
            _settings.RecentFloatingFocusBackgrounds.Insert(0, path);
            while (_settings.RecentFloatingFocusBackgrounds.Count > MaxRecentBackgrounds) _settings.RecentFloatingFocusBackgrounds.RemoveAt(_settings.RecentFloatingFocusBackgrounds.Count - 1);
        }
        Settings.Save(_settings);
        RenderFloatingFocusStatus();
    }

    private void FloatingFocusBrowse_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "Images ou vidéos (*.png;*.jpg;*.jpeg;*.bmp;*.mp4)|*.png;*.jpg;*.jpeg;*.bmp;*.mp4" };
        if (dialog.ShowDialog() != true) return;
        SelectFloatingBackground(dialog.FileName, remember: true);
    }

    private void FloatingFocusClear_Click(object sender, RoutedEventArgs e)
    {
        _settings.FloatingFocusBackgroundPath = null;
        Settings.Save(_settings);
        RenderFloatingFocusStatus();
    }

    private void FloatingFocusBlurSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_loaded) return;
        _settings.FloatingFocusBlur = e.NewValue;
        Settings.Save(_settings);
    }

    private void RefreshStartupStatus()
    {
        var enabled = IsStartupEnabled();
        StartupToggle.IsChecked = enabled;
        StartupStatusText.Text = enabled ? Loc.T("settings.startup.on") : Loc.T("settings.startup.off");
    }

    private static bool IsStartupEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
        return key?.GetValue(RunValueName) != null;
    }

    private void StartupToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (!_loaded) return;
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
        if (key == null) return;

        if (StartupToggle.IsChecked == true)
        {
            var exePath = Process.GetCurrentProcess().MainModule?.FileName;
            if (exePath != null) key.SetValue(RunValueName, $"\"{exePath}\"");
        }
        else
        {
            key.DeleteValue(RunValueName, throwOnMissingValue: false);
        }
        RefreshStartupStatus();
    }

    private void SettingsPage_Loaded(object sender, RoutedEventArgs e)
    {
        if (Application.Current is App app) app.UpdateStatusChanged += App_UpdateStatusChanged;
        RefreshUpdateStatus();
    }

    private void SettingsPage_Unloaded(object sender, RoutedEventArgs e)
    {
        if (Application.Current is App app) app.UpdateStatusChanged -= App_UpdateStatusChanged;
    }

    private void App_UpdateStatusChanged()
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(RefreshUpdateStatus);
            return;
        }
        RefreshUpdateStatus();
    }

    private void RefreshUpdateStatus()
    {
        if (Application.Current is not App app) return;
        var status = app.UpdateStatus;
        UpdateProgressBar.Visibility = status.Phase is UpdatePhase.Checking or UpdatePhase.Downloading or UpdatePhase.Installing
            ? Visibility.Visible : Visibility.Collapsed;
        UpdateProgressBar.IsIndeterminate = status.Phase == UpdatePhase.Checking;
        UpdateProgressBar.Value = status.Progress * 100;
        UpdateButton.IsEnabled = status.Phase is not (UpdatePhase.Checking or UpdatePhase.Downloading or UpdatePhase.Installing);

        switch (status.Phase)
        {
            case UpdatePhase.Checking:
                UpdateStatusText.Text = Loc.T("settings.update.checking");
                UpdateButton.Content = Loc.T("settings.update.checking.button");
                break;
            case UpdatePhase.UpToDate:
                UpdateStatusText.Text = string.Format(Loc.T("settings.update.current"), status.CurrentVersion);
                UpdateButton.Content = Loc.T("settings.update.check");
                break;
            case UpdatePhase.Available:
                UpdateStatusText.Text = string.Format(Loc.T("settings.update.available"), status.LatestVersion);
                UpdateButton.Content = Loc.T("settings.update.install");
                break;
            case UpdatePhase.Downloading:
                UpdateStatusText.Text = string.Format(Loc.T("settings.update.downloading"), Math.Round(status.Progress * 100));
                UpdateButton.Content = Loc.T("settings.update.install");
                break;
            case UpdatePhase.Installing:
                UpdateStatusText.Text = Loc.T("settings.update.installing");
                UpdateButton.Content = Loc.T("settings.update.install");
                break;
            case UpdatePhase.Failed:
                UpdateStatusText.Text = Loc.T("settings.update.failed");
                UpdateButton.Content = Loc.T("settings.update.retry");
                break;
            default:
                UpdateStatusText.Text = string.Format(Loc.T("settings.update.current"), status.CurrentVersion);
                UpdateButton.Content = Loc.T("settings.update.check");
                break;
        }
    }

    private async void UpdateButton_Click(object sender, RoutedEventArgs e)
    {
        if (Application.Current is App app) await app.CheckForUpdatesAsync(userInitiated: true);
    }

    private void NotificationsLink_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo { FileName = "ms-settings:notifications", UseShellExecute = true });
        }
        catch
        {
            // Windows Settings indisponible/désactivé sur cette machine : pas bloquant
        }
    }

    private void BrowserExtensionInstall_Click(object sender, RoutedEventArgs e)
    {
        if (!BrowserIntegration.RegisterNativeHost())
        {
            BrowserExtensionStatusText.Text = Loc.T("settings.extension.error");
            return;
        }
        BrowserIntegration.OpenStorePage();
        BrowserExtensionStatusText.Text = Loc.T("settings.extension.ready");
    }

    private void ClearHistory_Click(object sender, RoutedEventArgs e)
    {
        var result = System.Windows.MessageBox.Show(
            Loc.T("settings.privacy.confirm.message"),
            Loc.T("settings.privacy.confirm.title"),
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (result != MessageBoxResult.Yes) return;

        History.Clear();
    }
}
