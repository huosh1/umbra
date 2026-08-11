namespace Umbra.Core;

// Chemins de données - dossier distinct de l'app Electron existante
// (%AppData%\Umbra\data) pour ne jamais risquer de collision/corruption
// pendant que les deux versions coexistent.
public static class Config
{
    private static string _dataDir;

    // Modifiable (tests uniquement en pratique) pour isoler chaque test sur
    // son propre dossier temporaire - miroir du stub-electron.js côté JS qui
    // redirige DATA_DIR vers un dossier jetable pendant les tests.
    public static string DataDir
    {
        get => _dataDir;
        set
        {
            _dataDir = value;
            Directory.CreateDirectory(_dataDir);
        }
    }

    public static string SessionFile => Path.Combine(DataDir, "session.json");
    public static string HistoryFile => Path.Combine(DataDir, "history.json");
    public static string PeriodsFile => Path.Combine(DataDir, "periods.json");
    public static string BlocklistFile => Path.Combine(DataDir, "blocklist.json");
    public static string SavedBlocklistsFile => Path.Combine(DataDir, "saved_blocklists.json");
    public static string MusicHistoryFile => Path.Combine(DataDir, "music_history.json");
    public static string BlockAttemptsFile => Path.Combine(DataDir, "block_attempts.json");
    public static string SettingsFile => Path.Combine(DataDir, "settings.json");
    public static string HostsBackup => Path.Combine(DataDir, "hosts.backup");
    public static string LogFile => Path.Combine(DataDir, "watchdog.log");
    public static string WatchdogPidFile => Path.Combine(DataDir, "watchdog.pid");
    public static string WatchdogStopRequestFile => Path.Combine(DataDir, "watchdog.stop-request");
    public static string WatchdogStoppedFile => Path.Combine(DataDir, "watchdog.stopped");
    public static string BrowserHostStopRequestFile => Path.Combine(DataDir, "browser-host.stop-request");
    // Version attendue écrite juste avant de lancer l'installateur silencieux
    // (voir App.xaml.cs:CheckForUpdatesAsync) - lue et effacée au démarrage
    // suivant pour détecter un installateur qui s'est terminé "avec succès"
    // sans réellement remplacer les fichiers (process bloquant non fermé,
    // /SUPPRESSMSGBOXES masquant l'échec).
    public static string PendingUpdateVersionFile => Path.Combine(DataDir, "pending-update-version.txt");
    public static string LogsDir => Path.Combine(DataDir, "logs");
    public static string VocabDir => Path.Combine(DataDir, "vocab");
    public static string VocabProgressFile => Path.Combine(DataDir, "vocab_progress.json");

    // Banque de mots par défaut (fichiers statiques embarqués avec l'app,
    // copiés vers VocabDir au premier lancement - voir Vocab.SeedDefaults).
    // En dev, à côté du binaire ; une fois publié, ce dossier est copié dans
    // la sortie de build (voir Umbra.App.csproj, ItemGroup Content).
    public static string VocabDefaultsDir => Path.Combine(AppContext.BaseDirectory, "vocab-defaults");

    // Binaire + modèle Piper (TTS coréen hors-ligne), embarqués avec l'app -
    // même disposition que VocabDefaultsDir ci-dessus.
    public static string PiperDir => Path.Combine(AppContext.BaseDirectory, "piper");

    // Modifiable pour les tests (jamais question d'écrire dans le vrai
    // fichier hosts système pendant un test) - même raisonnement que
    // DataDir ci-dessus.
    public static string HostsPath { get; set; } = @"C:\Windows\System32\drivers\etc\hosts";
    // Marqueurs et nom de règle pare-feu distincts de ceux de l'app Electron
    // (Zix) - au cas où les deux tourneraient un jour côte à côte sur la
    // même machine, chacune ne doit jamais toucher au bloc de l'autre.
    public const string MarkStart = "# --- UMBRANATIVE BLOCK START ---";
    public const string MarkEnd = "# --- UMBRANATIVE BLOCK END ---";
    public const string FirewallRuleName = "UmbraNativeBlockSecureDNS";

    public static readonly string[] DohBlockIps =
    {
        "1.1.1.1", "1.0.0.1", // Cloudflare
        "8.8.8.8", "8.8.4.4", // Google
        "9.9.9.9", "149.112.112.112", // Quad9
    };

    public static readonly HashSet<string> ProtectedProcesses = new(StringComparer.OrdinalIgnoreCase)
    {
        "system", "system idle process", "registry", "idle",
        "smss.exe", "csrss.exe", "wininit.exe", "winlogon.exe", "services.exe",
        "lsass.exe", "svchost.exe", "dwm.exe", "explorer.exe",
        "umbra.exe", "umbranative.exe", "dotnet.exe",
        "fontdrvhost.exe", "sihost.exe", "taskhostw.exe", "ctfmon.exe", "conhost.exe",
    };

    static Config()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        _dataDir = Path.Combine(appData, "UmbraNative", "data");
        Directory.CreateDirectory(_dataDir);
    }
}
