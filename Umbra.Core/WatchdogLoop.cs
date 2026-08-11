namespace Umbra.Core;

// Boucle d'application des blocages, utilisée par le process watchdog élevé
// (Umbra.App.exe --watchdog). Le blocage doit être actif si UNE session
// manuelle (custom, ou pomodoro en phase "travail") est en cours, OU si
// l'horloge murale tombe dans une plage récurrente activée - ces deux
// sources sont indépendantes et se cumulent.
//
// Pas de dépendance UI ici à dessein (toasts natifs = Umbra.App) : le
// consommateur fournit un callback de notification ("done"/"break"/"work").
public static class WatchdogLoop
{
    public const int PollMs = 2000;

    public static void Log(string message)
    {
        var line = $"{DateTime.UtcNow:o} {message}\n";
        try
        {
            File.AppendAllText(Config.LogFile, line);
        }
        catch
        {
            // pas grave si le log échoue, l'enforcement continue
        }
    }

    // process.kill(pid, 0) depuis un process non élevé n'est pas fiable pour
    // vérifier si CE watchdog (élevé) est vivant - Windows peut refuser la
    // requête à travers la frontière d'élévation même si le process tourne
    // bel et bien. On préfère donc un heartbeat : le fichier pid est réécrit
    // à chaque tick, et le GUI juge le watchdog vivant si ce fichier a été
    // touché récemment, peu importe qui peut ou non interroger le process
    // par PID.
    public static void TouchHeartbeat()
    {
        try
        {
            File.WriteAllText(Config.WatchdogPidFile, Environment.ProcessId.ToString());
        }
        catch (Exception err)
        {
            Log($"ERROR heartbeat write failed: {err.Message}");
        }
    }

    public class Enforcer
    {
        // Un blocage (site et/ou app) a été tenté pour la fenêtre d'activité
        // en cours (session ou période), donc il faudra nettoyer (hosts +
        // pare-feu) quand elle se termine.
        // La règle pare-feu ne doit être (re)posée qu'une fois.
        private bool _dohBlockApplied;

        // État observé au tick précédent, pour détecter les transitions à
        // notifier (travail -> pause, pause -> travail, session -> terminée)
        // sans jamais notifier au tout premier tick (une session déjà en
        // cours au démarrage du watchdog ne doit pas déclencher un faux
        // "session terminée").
        private bool _notifInitialized;
        private bool _lastActive;
        private string? _lastKind;
        private string? _lastPhase;

        private readonly Action<string> _notify; // "done" | "break" | "work"

        public Enforcer(Action<string> notify)
        {
            _notify = notify;
        }

        public async Task CleanupAsync()
        {
            try
            {
                Blocker.RemoveSiteBlock();
            }
            catch (Exception err)
            {
                Log($"ERROR failed to remove site block: {err.Message}");
            }
            try
            {
                await Blocker.RemoveDohBlockAsync();
            }
            catch (Exception err)
            {
                Log($"ERROR failed to remove doh block: {err.Message}");
            }
            _dohBlockApplied = false;
        }

        public async Task TickAsync()
        {
            var s = Session.Load(); // fait aussi avancer les phases pomodoro dues
            var periodsData = Periods.Load();
            var activePeriods = Periods.GetActivePeriods(periodsData, DateTime.Now);
            var sessionBlocking = Session.IsBlockingActive(s);
            var shouldBlock = sessionBlocking || activePeriods.Count > 0;

            if (shouldBlock)
            {
                // Union des listes de toutes les sources actuellement
                // actives : la session manuelle utilise la blocklist
                // globale, chaque période active apporte en plus ses
                // propres listes apps/sites.
                var apps = new HashSet<string>();
                var sites = new HashSet<string>();
                if (sessionBlocking)
                {
                    try
                    {
                        var globalBl = Blocklist.Load();
                        foreach (var a in globalBl.Apps) apps.Add(a);
                        foreach (var st in globalBl.Sites) sites.Add(st);
                    }
                    catch (Exception err)
                    {
                        Log($"ERROR loading blocklist: {err.Message}");
                    }
                }
                foreach (var p in activePeriods)
                {
                    foreach (var a in p.Apps) apps.Add(a);
                    foreach (var st in p.Sites) sites.Add(st);
                }

                try
                {
                    Blocker.ApplySiteBlock(sites);
                }
                catch (Exception err)
                {
                    Log($"ERROR site block failed: {err.Message}");
                }

                if (!_dohBlockApplied)
                {
                    try
                    {
                        await Blocker.ApplyDohBlockAsync();
                        _dohBlockApplied = true;
                    }
                    catch (Exception err)
                    {
                        Log($"ERROR doh block failed: {err.Message}");
                    }
                }

                try
                {
                    foreach (var killedApp in Blocker.EnforceAppBlock(apps))
                    {
                        BlockAttemptHistory.Record(killedApp);
                    }
                }
                catch (Exception err)
                {
                    Log($"ERROR app block failed: {err.Message}");
                }
            }
            else
            {
                // Toujours tenté, même si _blocksActive est déjà false : ce
                // champ ne vit qu'en mémoire pour CE process. Si un watchdog
                // précédent est mort/a été tué pendant qu'un blocage était
                // actif (hosts pas nettoyé), le nouveau watchdog démarre
                // avec _blocksActive=false et ne saurait jamais qu'il doit
                // nettoyer un blocage orphelin s'il ne tentait le retrait
                // que sur cette condition - or RemoveSiteBlock()/
                // RemoveDohBlockAsync() sont sans effet s'il n'y a déjà rien
                // à retirer, donc appeler à chaque tick où rien ne doit
                // bloquer est sans coût et garantit qu'un blocage orphelin
                // se résorbe de lui-même en un tick au lieu de rester
                // bloqué à vie.
                await CleanupAsync();
            }

            // Fin de vie d'une session "custom" expirée : indépendant du
            // fait que les blocages viennent d'être enlevés ou pas (couvre
            // aussi le cas d'un watchdog qui hérite d'une session déjà
            // marquée active mais expirée). Une session pomodoro se termine
            // toute seule via Session.Load() - rien à faire ici pour elle
            // (une pause n'est pas une fin de session).
            if (s.Kind == "custom" && s.Active && Session.RemainingSeconds(s) <= 0)
            {
                var durationMinutes = (s.EndTs - s.StartTs) / 60000.0;
                Log($"session terminee ({durationMinutes:F1} min)");
                Session.Stop(s); // met s.Active à false en place
            }

            var curPhase = s.Kind == "pomodoro" && s.Pomodoro != null ? s.Pomodoro.Phase : null;
            if (_notifInitialized)
            {
                if (_lastActive && !s.Active)
                {
                    _notify("done");
                }
                else if (_lastActive && s.Active && _lastKind == "pomodoro" && s.Kind == "pomodoro" && _lastPhase != null && curPhase != null && _lastPhase != curPhase)
                {
                    _notify(curPhase == "break" ? "break" : "work");
                }
            }
            _notifInitialized = true;
            _lastActive = s.Active;
            _lastKind = s.Kind;
            _lastPhase = curPhase;
        }
    }

    public static async Task RunAsync(Action<string> notify, CancellationToken cancellationToken)
    {
        Log("watchdog started");
        TouchHeartbeat();
        var enforcer = new Enforcer(notify);

        while (!cancellationToken.IsCancellationRequested)
        {
            if (File.Exists(Config.WatchdogStopRequestFile))
            {
                Log("watchdog stopping for update");
                await enforcer.CleanupAsync();
                try { File.Delete(Config.WatchdogStopRequestFile); } catch { }
                try { File.Delete(Config.WatchdogPidFile); } catch { }
                try { File.WriteAllText(Config.WatchdogStoppedFile, DateTime.UtcNow.ToString("O")); } catch { }
                return;
            }

            TouchHeartbeat();
            try
            {
                await enforcer.TickAsync();
            }
            catch (Exception err)
            {
                Log($"ERROR unhandled: {err.Message}");
            }
            try
            {
                await Task.Delay(PollMs, cancellationToken);
            }
            catch (TaskCanceledException)
            {
                break;
            }
        }
    }
}
