using System.Diagnostics;
using System.IO;
using Umbra.Core;

namespace Umbra.App;

// Partagé entre FocusPage (démarrage d'une session) et App.xaml.cs
// (démarrage rapide depuis le systray) - un seul point de vérité pour
// savoir si le watchdog élevé tourne et, sinon, le relancer via UAC.
internal static class WatchdogSupervisor
{
    // process.kill(pid,0) n'est pas fiable pour vérifier si le watchdog
    // (élevé) est vivant depuis ce process (non élevé) - même raisonnement
    // que côté Electron : on se fie au heartbeat (fichier pid retouché à
    // chaque tick), pas à une requête directe par PID.
    public static bool IsAlive()
    {
        if (!File.Exists(Config.WatchdogPidFile)) return false;
        var age = DateTime.UtcNow - File.GetLastWriteTimeUtc(Config.WatchdogPidFile);
        return age.TotalMilliseconds < WatchdogLoop.PollMs * 4;
    }

    public static void Ensure()
    {
        if (IsAlive()) return;
        DeleteSignalFile(Config.WatchdogStopRequestFile);
        DeleteSignalFile(Config.WatchdogStoppedFile);
        try
        {
            var exePath = Process.GetCurrentProcess().MainModule?.FileName;
            if (exePath == null) return;
            Process.Start(new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = "--watchdog",
                UseShellExecute = true,
                Verb = "runas", // déclenche l'invite UAC - échappatoire assumée du hard mode : le tuer depuis le Gestionnaire des tâches nécessite ces droits
            });
        }
        catch
        {
            // UAC refusée ou autre échec : pas de blocage tant que l'utilisateur ne relance pas manuellement
        }
    }

    public static async Task<bool> StopForUpdateAsync(CancellationToken cancellationToken = default)
    {
        if (!IsAlive())
        {
            DeleteSignalFile(Config.WatchdogStopRequestFile);
            DeleteSignalFile(Config.WatchdogStoppedFile);
            return true;
        }

        DeleteSignalFile(Config.WatchdogStoppedFile);
        try
        {
            File.WriteAllText(Config.WatchdogStopRequestFile, DateTime.UtcNow.ToString("O"));
        }
        catch
        {
            return false;
        }

        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
        while (DateTime.UtcNow < deadline)
        {
            if (File.Exists(Config.WatchdogStoppedFile))
            {
                DeleteSignalFile(Config.WatchdogStoppedFile);
                return true;
            }
            await Task.Delay(200, cancellationToken);
        }

        DeleteSignalFile(Config.WatchdogStopRequestFile);
        return !IsAlive();
    }

    private static void DeleteSignalFile(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch
        {
        }
    }
}
