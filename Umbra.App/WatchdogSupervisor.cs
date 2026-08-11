using System.Diagnostics;
using System.IO;
using System.Linq;
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
        if (IsAlive())
        {
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
            var stoppedCooperatively = false;
            while (DateTime.UtcNow < deadline)
            {
                if (File.Exists(Config.WatchdogStoppedFile))
                {
                    DeleteSignalFile(Config.WatchdogStoppedFile);
                    stoppedCooperatively = true;
                    break;
                }
                await Task.Delay(200, cancellationToken);
            }

            DeleteSignalFile(Config.WatchdogStopRequestFile);
            if (!stoppedCooperatively && IsAlive()) return false;
        }
        else
        {
            DeleteSignalFile(Config.WatchdogStopRequestFile);
            DeleteSignalFile(Config.WatchdogStoppedFile);
        }

        // Le suivi ci-dessus ne repose que sur le heartbeat de watchdog.pid : un
        // process Umbra.exe planté/orphelin (heartbeat mort mais process encore
        // vivant côté OS, notamment un ancien watchdog élevé jamais nettoyé) lui
        // échappe totalement. Restart Manager de l'installeur ne peut pas fermer
        // de force un process de plus haute intégrité depuis un contexte non-élevé
        // - mieux vaut échouer ici, visiblement, que laisser l'installeur silencieux
        // (/SUPPRESSMSGBOXES) ignorer le blocage et ne rien installer du tout.
        return !HasOtherUmbraProcesses();
    }

    private static bool HasOtherUmbraProcesses()
    {
        var currentId = Environment.ProcessId;
        var others = Process.GetProcessesByName("Umbra");
        try
        {
            foreach (var other in others)
            {
                if (other.Id == currentId) continue;
                try
                {
                    other.Kill();
                    other.WaitForExit(2000);
                }
                catch
                {
                    // Accès refusé (process élevé) ou déjà sorti entre-temps - dans
                    // le premier cas le process survit, détecté au recomptage.
                }
            }
        }
        finally
        {
            foreach (var other in others) other.Dispose();
        }

        var recheck = Process.GetProcessesByName("Umbra");
        try
        {
            return recheck.Any(p => p.Id != currentId);
        }
        finally
        {
            foreach (var p in recheck) p.Dispose();
        }
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
