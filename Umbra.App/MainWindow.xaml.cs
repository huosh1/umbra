using System.IO;
using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using Umbra.App.Pages;
using Umbra.Core;
using Wpf.Ui.Controls;

namespace Umbra.App;

public partial class MainWindow : FluentWindow
{
    private string _activeTag = "focus";
    private static readonly Dictionary<string, string> SearchIndex = new()
    {
        ["focus"] = "focus session sessions pomodoro pomodoro libre free schedule schedules planning minuteur timer hard mode blocklist tâche task",
        ["blocklist"] = "blocklist blocklists blocage bloquer sites websites applications apps profils profiles distractions ready made",
        ["stats"] = "stats statistics statistiques streak activité activity calendar calendrier top played sounds musique music titres écoutes blocked attempts tentatives",
        ["sounds"] = "sounds sons ambiance ambient birds waterfall rain thunder fireplace bruit blanc white noise",
        ["settings"] = "settings réglages parametres theme thème appearance apparence dark sombre light clair background fond blur flou floating timer minuteur flottant spotify notifications language langue extension privacy confidentialité startup démarrage reminders rappels",
    };

    public MainWindow()
    {
        InitializeComponent();
        Title = "Umbra";

        if (Periods.HasEnabledPeriod(Periods.Load()))
        {
            WatchdogSupervisor.Ensure();
        }

        ApplyLanguage();
        Loc.LanguageChanged += ApplyLanguage;
        Closed += (_, _) => Loc.LanguageChanged -= ApplyLanguage;
        ApplyBackgroundImage(Settings.Load().BackgroundImagePath);

        // ReplaceContent a besoin que le template du contrôle (Frame interne)
        // soit déjà appliqué - l'appeler depuis le constructeur (avant que la
        // fenêtre soit chargée) lève une NullReferenceException.
        Loaded += (_, _) => RootNavigation.ReplaceContent(PadPage(new FocusPage()), null);
    }

    // Image de fond personnalisée (Réglages > Background image) - floutée et
    // posée derrière NavigationView (voir BackgroundImageElement dans le
    // XAML), avec les fonds de l'UI rendus semi-transparents pendant qu'elle
    // est active (AppTheme.SetBackgroundTranslucent) pour qu'elle
    // transparaisse, comme sur Ambie.
    public void ApplyBackgroundImage(string? path)
    {
        var settings = Settings.Load();
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            BackgroundImageElement.Visibility = Visibility.Collapsed;
            BackgroundImageElement.Source = null;
            AppTheme.SetBackgroundActive(false, settings.BackgroundOverlayOpacity, settings.BackgroundAppearanceMode);
            SolidNavigationPaneLayer.Visibility = Visibility.Collapsed;
            return;
        }

        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.UriSource = new Uri(path, UriKind.Absolute);
        bitmap.EndInit();
        bitmap.Freeze();

        BackgroundImageElement.Source = bitmap;
        BackgroundImageElement.Effect = new BlurEffect { Radius = settings.BackgroundBlur, KernelType = KernelType.Gaussian };
        BackgroundImageElement.Visibility = Visibility.Visible;
        AppTheme.SetBackgroundActive(true, settings.BackgroundOverlayOpacity, settings.BackgroundAppearanceMode);
        SolidNavigationPaneLayer.Visibility = settings.BackgroundAppearanceMode == "content"
            ? Visibility.Visible : Visibility.Collapsed;
    }

    // Appelé en direct par le Slider de transparence des Réglages, sans
    // recharger l'image (juste le calque de couleur devant qui bouge).
    public void SetBackgroundOpacity(double opacity)
    {
        if (BackgroundImageElement.Visibility != Visibility.Visible) return;
        var settings = Settings.Load();
        AppTheme.SetBackgroundActive(true, opacity, settings.BackgroundAppearanceMode);
    }

    public void RefreshBackgroundAppearance() => ApplyBackgroundImage(Settings.Load().BackgroundImagePath);

    // Marge uniforme autour du contenu de Focus/Blocages/Statistiques, posée
    // une seule fois ici plutôt que dans chaque .xaml - depuis la suppression
    // de la barre de titre séparée, le Frame de NavigationView colle
    // directement sous NowPlayingBar sans plus aucun espace de respiration
    // au-dessus. Réglages n'est PAS concerné (demande explicite : ne pas
    // toucher à cet onglet).
    private static UIElement PadPage(UserControl page) =>
        new Border { Padding = new Thickness(24, 20, 24, 24), Child = page };

    // Applique les libellés dans la langue courante - appelé une fois au
    // démarrage et à chaque changement de langue (Loc.LanguageChanged),
    // puisque la barre latérale n'est jamais recréée comme le sont les pages.
    private void ApplyLanguage()
    {
        SearchBox.PlaceholderText = Loc.T("search.placeholder");
        FocusNavItem.Content = Loc.T("nav.focus");
        BlocklistNavItem.Content = Loc.T("nav.blocklist");
        StatsNavItem.Content = Loc.T("nav.stats");
        SoundsNavItem.Content = Loc.T("nav.sounds");
        SettingsNavItem.Content = Loc.T("nav.settings");

        // La page actuellement affichée contient elle aussi du texte fixe -
        // la reconstruire la fait se re-rendre dans la nouvelle langue.
        if (IsLoaded) NavigateTo(_activeTag);
    }

    // NavigationView.SelectionChanged s'est avéré non fiable au clic (le
    // MouseDown initial est absorbé par un Grid interne du contrôle avant
    // d'atteindre l'item, empêchant le geste de clic de se compléter -
    // confirmé par traçage d'événements). PreviewMouseLeftButtonUp posé sur
    // chaque item, lui, se déclenche de façon fiable - c'est ce qui pilote
    // la navigation ici.
    private void NavItem_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is not NavigationViewItem clicked || clicked.Tag is not string tag) return;
        NavigateTo(tag);
    }

    private void NavigateTo(string tag, string? searchQuery = null)
    {
        _activeTag = tag;

        foreach (var obj in RootNavigation.MenuItems)
        {
            if (obj is NavigationViewItem item) item.IsActive = item.Tag as string == tag;
        }
        foreach (var obj in RootNavigation.FooterMenuItems)
        {
            if (obj is NavigationViewItem item) item.IsActive = item.Tag as string == tag;
        }

        UserControl page = tag switch
        {
            "focus" => new FocusPage(),
            "blocklist" => new BlocklistPage(),
            "stats" => new StatsPage(),
            "sounds" => new SoundsPage(),
            "settings" => new SettingsPage(),
            _ => new FocusPage(),
        };
        if (page is SettingsPage settingsPage && !string.IsNullOrWhiteSpace(searchQuery)) settingsPage.ApplySearch(searchQuery);
        RootNavigation.ReplaceContent(PadPage(page), null);
    }

    // Filtre les items de la barre latérale par libellé (langue courante) au
    // fur et à mesure de la frappe - seule fonctionnalité de recherche utile
    // ici vu le peu d'onglets, mais évite d'avoir un champ purement décoratif.
    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        var query = SearchBox.Text.Trim();
        if (query.Length == 0)
        {
            foreach (var obj in RootNavigation.MenuItems) if (obj is NavigationViewItem item) item.Visibility = Visibility.Visible;
            foreach (var obj in RootNavigation.FooterMenuItems) if (obj is NavigationViewItem item) item.Visibility = Visibility.Visible;
            if (IsLoaded) NavigateTo(_activeTag);
            return;
        }
        var matches = new List<string>();
        foreach (var obj in RootNavigation.MenuItems)
        {
            if (obj is NavigationViewItem item)
            {
                var visible = Matches(item, query);
                item.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
                if (visible && query.Length > 0 && item.Tag is string tag) matches.Add(tag);
            }
        }
        foreach (var obj in RootNavigation.FooterMenuItems)
        {
            if (obj is NavigationViewItem item)
            {
                var visible = Matches(item, query);
                item.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
                if (visible && query.Length > 0 && item.Tag is string tag) matches.Add(tag);
            }
        }
        if (matches.Distinct().ToList() is { Count: 1 } unique) NavigateTo(unique[0], query);
    }

    private static bool Matches(NavigationViewItem item, string query)
    {
        if (query.Length == 0) return true;
        var tag = item.Tag as string ?? "";
        var searchable = $"{item.Content} {SearchIndex.GetValueOrDefault(tag, "")}";
        return NormalizeSearchText(searchable).Contains(NormalizeSearchText(query), StringComparison.Ordinal);
    }

    private static string NormalizeSearchText(string value)
    {
        var decomposed = value.ToLowerInvariant().Normalize(NormalizationForm.FormD);
        return new string(decomposed.Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark).ToArray());
    }

    private void SearchBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;

        foreach (var obj in RootNavigation.MenuItems)
        {
            if (obj is NavigationViewItem { Visibility: Visibility.Visible, Tag: string tag })
            {
                NavigateTo(tag);
                return;
            }
        }
        foreach (var obj in RootNavigation.FooterMenuItems)
        {
            if (obj is NavigationViewItem { Visibility: Visibility.Visible, Tag: string tag })
            {
                NavigateTo(tag);
                return;
            }
        }
    }
}
