using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Wpf.Ui.Controls;

namespace Umbra.App.Pages;

public partial class SoundsPage : UserControl
{
    public SoundsPage()
    {
        InitializeComponent();
        TitleText.Text = Loc.T("sounds.title");
        SubtitleText.Text = Loc.T("sounds.subtitle");
        RenderCatalog();
        AmbientSoundService.Changed += OnChanged;
        Unloaded += (_, _) => AmbientSoundService.Changed -= OnChanged;
    }

    private void OnChanged() => Dispatcher.Invoke(RenderCatalog);

    private void RenderCatalog()
    {
        CatalogPanel.Children.Clear();
        foreach (var sound in AmbientSoundService.Catalog)
        {
            var active = AmbientSoundService.IsActive(sound.Id);
            var image = new ImageBrush(new BitmapImage(new Uri(AmbientSoundService.ImagePath(sound)))) { Stretch = Stretch.UniformToFill };
            var overlay = new Border { Background = new LinearGradientBrush(Colors.Transparent, Color.FromArgb(190, 0, 0, 0), 90) };
            var label = new System.Windows.Controls.TextBlock { Text = sound.Name, Foreground = Brushes.White, FontWeight = FontWeights.SemiBold, Margin = new Thickness(10), VerticalAlignment = VerticalAlignment.Bottom };
            var status = new SymbolIcon { Symbol = active ? SymbolRegular.Pause24 : SymbolRegular.Play24, Foreground = Brushes.White, FontSize = 16, Margin = new Thickness(10), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top };
            var grid = new Grid();
            grid.Children.Add(overlay); grid.Children.Add(label); grid.Children.Add(status);
            var card = new Border
            {
                Width = 142, Height = 190, Margin = new Thickness(0, 0, 10, 10),
                BorderThickness = new Thickness(active ? 2 : 0), BorderBrush = (Brush)FindResource("AccentFillColorDefaultBrush"),
                CornerRadius = new CornerRadius(7), ClipToBounds = true,
                Background = image, Child = grid, Cursor = System.Windows.Input.Cursors.Hand, ToolTip = active ? "Playing" : "Play"
            };
            card.MouseLeftButtonUp += (_, _) =>
            {
                if (!AmbientSoundService.Toggle(sound))
                    System.Windows.MessageBox.Show(Loc.T("sounds.limit"), "Umbra", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            };
            CatalogPanel.Children.Add(card);
        }
    }
}
