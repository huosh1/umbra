using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Umbra.App;

// Barre de titre sombre native (Windows 11+), assortie au thème Ambie
// (jenius-apps/ambie). API stable et documentée (dwmapi.dll, utilisée par
// l'Explorateur/le Menu Démarrer), sans rapport avec le Bootstrapper
// WindowsAppSDK à l'origine des crashs natifs qui ont fait abandonner
// WinUI3 (voir Umbra.Launcher.retired.bak) - no-op silencieux sur Windows 10.
//
// Note : DWMWA_SYSTEMBACKDROP_TYPE (Acrylic/Mica natif) a été essayé et
// abandonné ici - le compositeur WPF ne blend pas proprement avec le
// backdrop DWM dans la zone client (fond noir opaque au lieu du flou),
// seule la barre de titre en profite, avec un résultat visuel incohérent
// (bande teintée par l'accent système au-dessus d'un panneau uni). La
// couleur de fond Ambie (#1E1E1E, voir BgBrush) reproduit fidèlement le
// thème sans dépendre de ce comportement non fiable.
internal static partial class DwmBackdrop
{
    private const int DwmwaUseImmersiveDarkMode = 20;

    [LibraryImport("dwmapi.dll")]
    private static partial int DwmSetWindowAttribute(IntPtr hwnd, int dwAttribute, ref int pvAttribute, int cbAttribute);

    public static void Apply(Window window)
    {
        try
        {
            var hwnd = new WindowInteropHelper(window).EnsureHandle();
            var darkMode = 1;
            DwmSetWindowAttribute(hwnd, DwmwaUseImmersiveDarkMode, ref darkMode, sizeof(int));
        }
        catch
        {
            // Windows 10 ou attribut non supporté : reste sur la barre de titre par défaut, pas bloquant
        }
    }
}
