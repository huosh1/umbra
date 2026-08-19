namespace Umbra.Core;

// File.WriteAllText seul est un truncate-puis-write : un crash, une coupure
// disque plein, ou un kill forcé du watchdog pendant l'écriture (arrive
// réellement - voir WatchdogStopRequestFile) laisse un JSON tronqué. Chaque
// Load() de ce projet avale déjà l'exception de parsing et retombe sur un
// état par défaut vide, donc un fichier corrompu se traduit par une perte
// silencieuse et définitive (historique, blocklists, plages...) au prochain
// Save(). Écrire dans un .tmp puis renommer (déjà le pattern utilisé par
// MusicHistory.PersistCached) rend l'écriture atomique au niveau du
// système de fichiers : soit l'ancien contenu reste intact, soit le nouveau
// y est intégralement - jamais un état intermédiaire.
public static class AtomicFile
{
    public static void WriteAllText(string path, string content)
    {
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, content);
        File.Move(tmp, path, overwrite: true);
    }
}
