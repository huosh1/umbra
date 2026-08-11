using System.Windows;
using System.Windows.Controls;
using Umbra.Core;
using Wpf.Ui.Controls;
using TextBlock = System.Windows.Controls.TextBlock;

namespace Umbra.App;

public partial class RunningAppsPickerWindow : FluentWindow
{
    private readonly List<string> _allApps;
    public string? Selected { get; private set; }

    public RunningAppsPickerWindow()
    {
        InitializeComponent();
        Title = Loc.T("blocklist.pick.title");
        TitleText.Text = Loc.T("blocklist.pick.title");
        SearchBox.PlaceholderText = Loc.T("search.placeholder");
        CancelButton.Content = Loc.T("blocklist.pick.cancel");

        _allApps = RunningApps.GetVisibleAppNames();
        Render(_allApps);
    }

    private void Render(List<string> apps)
    {
        AppsList.Items.Clear();
        if (apps.Count == 0)
        {
            AppsList.Items.Add(new TextBlock { Text = Loc.T("blocklist.pick.empty"), Opacity = 0.6, Margin = new Thickness(0, 8, 0, 0) });
            return;
        }
        foreach (var app in apps)
        {
            var btn = new CardAction { Icon = new SymbolIcon { Symbol = SymbolRegular.Window24 }, Content = app, Margin = new Thickness(0, 0, 0, 6) };
            // Setting DialogResult closes a modal WPF window automatically.
            // Calling Close() again can throw after the window has already closed.
            btn.Click += (_, _) => { Selected = app; DialogResult = true; };
            AppsList.Items.Add(btn);
        }
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        var query = SearchBox.Text.Trim();
        Render(query.Length == 0 ? _allApps : _allApps.Where(a => a.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList());
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        // Setting DialogResult is enough to close a window shown with ShowDialog().
        DialogResult = false;
    }

    public static string? Pick(Window owner)
    {
        var window = new RunningAppsPickerWindow { Owner = owner };
        return window.ShowDialog() == true ? window.Selected : null;
    }
}
