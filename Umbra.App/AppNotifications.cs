using Microsoft.Toolkit.Uwp.Notifications;

namespace Umbra.App;

// TrayIcon.ShowNotification utilisait Shell_NotifyIcon (P/Invoke direct,
// voir TrayIcon.cs) : ces "balloons" n'ont pas d'identité d'app Windows
// (AUMID), donc Umbra n'apparaît jamais dans Réglages > Notifications, et
// Windows peut les faire disparaître silencieusement selon la config.
// ToastNotificationManagerCompat (API recommandée du paquet, contrairement
// à DesktopNotificationManagerCompat) gère l'identité AUMID pour une app
// Win32 non empaquetée (pas de MSIX) sans avoir besoin de retoucher le
// raccourci du menu Démarrer ni d'enregistrer de classe COM manuellement.
internal static class AppNotifications
{
    private static bool _initialized;

    public static void Initialize()
    {
        if (_initialized) return;
        _initialized = true;
        try
        {
            ToastNotificationManagerCompat.OnActivated += _ => { };
        }
        catch
        {
            // Environnement sans shell/registre accessible (rare) : les
            // notifications resteront simplement silencieuses plutôt que de
            // planter le démarrage de l'app pour ça.
        }
    }

    public static void Show(string title, string message)
    {
        try
        {
            new ToastContentBuilder()
                .AddText(title)
                .AddText(message)
                .Show();
        }
        catch
        {
            // Best-effort : une notification manquée ne doit jamais faire
            // planter le flux appelant (fin de session, mise à jour, etc.).
        }
    }
}
