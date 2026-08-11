using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Umbra.Core;
using Wpf.Ui.Controls;
using TextBlock = System.Windows.Controls.TextBlock;

namespace Umbra.App.Pages;

public partial class PeriodsPage : UserControl
{
    // Period.Days utilise DayOfWeek natif (0=dimanche..6=samedi) - Loc.DayLetters
    // est affiché lundi->dimanche, donc cette table convertit index affiché -> DayOfWeek.
    private static readonly int[] DayIndexToDow = [1, 2, 3, 4, 5, 6, 0];

    private PeriodsData _data = Periods.Load();
    private readonly HashSet<int> _selectedDays = new();
    private readonly List<ToggleButton> _dayButtons = new();
    private List<SavedBlocklist> _savedBlocklists = new();

    public PeriodsPage()
    {
        InitializeComponent();

        NewPeriodExpander.Header = Loc.T("periods.new");
        NameBox.Text = Loc.T("periods.name.default");
        StartLabel.Text = Loc.T("periods.start");
        EndLabel.Text = Loc.T("periods.end");
        DaysLabel.Text = Loc.T("periods.days");
        BlocklistLabel.Text = Loc.T("periods.blocklist.label");
        AddPeriodButton.Content = Loc.T("periods.add");

        BuildDayButtons();
        BuildBlocklistCombo();
        Render();
    }

    // "Aucune" (index 0) laisse la plage sans apps/sites propres (comme
    // avant) - choisir une liste enregistrée copie ses apps/sites dans la
    // plage à la création, exactement comme Period.Apps/Sites le permet déjà
    // côté watchdog (WatchdogLoop additionne les listes des plages actives).
    private void BuildBlocklistCombo()
    {
        _savedBlocklists = SavedBlocklists.Load();
        BlocklistCombo.Items.Clear();
        BlocklistCombo.Items.Add(Loc.T("periods.blocklist.none"));
        foreach (var list in _savedBlocklists) BlocklistCombo.Items.Add(list.Name);
        BlocklistCombo.SelectedIndex = 0;
    }

    private void BuildDayButtons()
    {
        DaysPanel.Children.Clear();
        _dayButtons.Clear();
        var dayLabels = Loc.DayLetters;
        for (var i = 0; i < dayLabels.Length; i++)
        {
            var dow = DayIndexToDow[i];
            var btn = new ToggleButton
            {
                Content = dayLabels[i],
                Width = 32,
                Height = 32,
                Margin = new Thickness(0, 0, 6, 0),
            };
            btn.Checked += (_, _) => _selectedDays.Add(dow);
            btn.Unchecked += (_, _) => _selectedDays.Remove(dow);
            _dayButtons.Add(btn);
            DaysPanel.Children.Add(btn);
        }
    }

    private void Render()
    {
        PeriodsList.Items.Clear();
        foreach (var p in _data.Periods) PeriodsList.Items.Add(BuildRow(p));
    }

    private CardControl BuildRow(Period p)
    {
        var enabledBox = new CheckBox
        {
            Content = p.Name,
            IsChecked = p.Enabled,
            VerticalAlignment = VerticalAlignment.Center,
        };
        enabledBox.Checked += (_, _) => { p.Enabled = true; Periods.Save(_data); };
        enabledBox.Unchecked += (_, _) => { p.Enabled = false; Periods.Save(_data); };

        var dayLabels = Loc.DayLetters;
        var days = string.Concat(Enumerable.Range(0, 7).Select(i => p.Days.Contains(DayIndexToDow[i]) ? dayLabels[i] : "·"));
        var blockCount = p.Apps.Count + p.Sites.Count;
        var blockSuffix = blockCount > 0 ? $"  ·  {blockCount}" : "";
        var detail = new TextBlock
        {
            Text = $"{p.StartTime}–{p.EndTime}  {days}{blockSuffix}",
            Opacity = 0.6,
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(10, 0, 0, 0),
        };

        var removeBtn = new Wpf.Ui.Controls.Button { Content = Loc.T("periods.remove"), Appearance = ControlAppearance.Danger };
        removeBtn.Click += (_, _) =>
        {
            _data.Periods.Remove(p);
            Periods.Save(_data);
            Render();
        };

        var left = new StackPanel { Orientation = Orientation.Horizontal };
        left.Children.Add(enabledBox);
        left.Children.Add(detail);

        return new CardControl { Header = left, Content = removeBtn, Margin = new Thickness(0, 0, 0, 6) };
    }

    private void AddPeriod_Click(object sender, RoutedEventArgs e)
    {
        var name = string.IsNullOrWhiteSpace(NameBox.Text) ? Loc.T("periods.name.default") : NameBox.Text.Trim();
        if (_selectedDays.Count == 0) return;

        var period = new Period
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = name,
            Enabled = true,
            Recurring = true,
            Days = _selectedDays.ToList(),
            StartTime = string.IsNullOrWhiteSpace(StartBox.Text) ? "00:00" : StartBox.Text.Trim(),
            EndTime = string.IsNullOrWhiteSpace(EndBox.Text) ? "00:00" : EndBox.Text.Trim(),
        };
        if (BlocklistCombo.SelectedIndex > 0)
        {
            var chosen = _savedBlocklists[BlocklistCombo.SelectedIndex - 1];
            period.Apps = new List<string>(chosen.Apps);
            period.Sites = new List<string>(chosen.Sites);
        }
        _data.Periods.Add(period);
        Periods.Save(_data);

        foreach (var b in _dayButtons) b.IsChecked = false;
        _selectedDays.Clear();
        BlocklistCombo.SelectedIndex = 0;
        Render();
    }
}
