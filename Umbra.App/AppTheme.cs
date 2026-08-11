using System.Windows.Media;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace Umbra.App;

// Bascule Dark/Light appliquée à l'appli entière - ApplicationThemeManager
// gère la partie WPF-UI (contrôles, ThemesDictionary), mais nos couleurs
// custom pixel-matchées sur Ambie (ApplicationBackgroundBrush,
// NavigationViewContentBackground - voir App.xaml) sont des overrides fixes
// qui ne suivent pas ce changement automatiquement : sans ce réglage manuel
// en plus, "Light" resterait à moitié sombre (contrôles clairs sur nos fonds
// toujours foncés).
//
// Garde aussi l'état "image de fond active ?" (Réglages > Background image) :
// quand une image de fond floutée est affichée derrière l'UI (voir
// MainWindow.BackgroundImageElement), ces deux mêmes brushes doivent devenir
// semi-transparentes pour laisser le flou transparaître, sinon l'image posée
// juste derrière serait entièrement cachée par des fonds 100% opaques.
// L'opacité exacte (curseur "Transparence" des Réglages) est réglable par
// l'utilisateur - 0 = fond très visible, 1 = comme sans image.
public static class AppTheme
{
    private static string _theme = "Dark";
    private static bool _backgroundActive;
    private static double _backgroundOpacity = 0.88;
    private static string _backgroundMode = "full";

    public static void Apply(string theme)
    {
        _theme = theme;
        var isLight = theme == "Light";
        ApplicationThemeManager.Apply(isLight ? ApplicationTheme.Light : ApplicationTheme.Dark, WindowBackdropType.Mica);
        ApplyBrushes();
    }

    public static void SetBackgroundActive(bool active, double opacity, string mode = "full")
    {
        _backgroundActive = active;
        _backgroundOpacity = opacity;
        _backgroundMode = mode;
        ApplyBrushes();
    }

    private static void ApplyBrushes()
    {
        var isLight = _theme == "Light";
        var appBackground = isLight ? "#F3F3F3" : "#1F1F25";
        var contentBackground = isLight ? "#FAFAFA" : "#121212";
        var translucent = (byte)(Math.Clamp(_backgroundOpacity, 0, 1) * 255);
        // Le brush racine couvre aussi le gutter entre le pane et le Frame.
        // Il doit donc toujours laisser passer l'image lorsqu'un fond est
        // actif. En mode "content", l'opacité de la sidebar est assurée par
        // SolidNavigationPaneLayer, limitée exactement au pane.
        var appAlpha = _backgroundActive ? translucent : (byte)0xFF;
        var contentAlpha = _backgroundActive && _backgroundMode != "navigation" ? translucent : (byte)0xFF;

        System.Windows.Application.Current.Resources["ApplicationBackgroundBrush"] = MakeBrush(appBackground, appAlpha);
        System.Windows.Application.Current.Resources["NavigationPaneSolidBackground"] = MakeBrush(appBackground, 0xFF);
        System.Windows.Application.Current.Resources["NavigationViewContentBackground"] = MakeBrush(contentBackground, contentAlpha);
    }

    private static SolidColorBrush MakeBrush(string hex, byte alpha)
    {
        var color = (Color)ColorConverter.ConvertFromString(hex);
        color.A = alpha;
        return new SolidColorBrush(color);
    }
}
