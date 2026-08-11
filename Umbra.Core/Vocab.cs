using System.Text.Json;
using System.Text.Json.Serialization;

namespace Umbra.Core;

public class VocabWord
{
    public string Id { get; set; } = "";
    public string Korean { get; set; } = "";
    public string Meaning { get; set; } = "";

    [JsonPropertyName("example_kr")]
    public string ExampleKr { get; set; } = "";

    [JsonPropertyName("example_fr")]
    public string ExampleFr { get; set; } = "";

    public string Level { get; set; } = "";
    public string Source { get; set; } = "";
    public string Status { get; set; } = "new"; // "new" | "review" | "mastered"
}

// Entrée brute telle que stockée dans data/vocab/*.json (avant fusion avec
// le statut de progression, qui vit à part dans vocab_progress.json).
internal class RawVocabEntry
{
    public string? Id { get; set; }
    public string? Korean { get; set; }
    public string? Meaning { get; set; }

    [JsonPropertyName("example_kr")]
    public string? ExampleKr { get; set; }

    [JsonPropertyName("example_fr")]
    public string? ExampleFr { get; set; }

    public string? Level { get; set; }
}

public class VocabStats
{
    public int Total { get; set; }
    public int New { get; set; }
    public int Review { get; set; }
    public int Mastered { get; set; }
}

public class ImportResult
{
    public int Count { get; set; }
    public string? File { get; set; }
}

public static class Vocab
{
    static Vocab()
    {
        Directory.CreateDirectory(Config.VocabDir);
    }

    // Le contenu (mot/sens/exemples) vit dans des fichiers statiques dans
    // VocabDir/*.json ; le statut (nouveau/à revoir/maîtrisé) vit à part
    // dans vocab_progress.json, indexé par id. Ça permet d'ajouter/regénérer
    // des lots de contenu sans jamais perdre la progression de l'utilisateur.

    // Copie les fichiers par défaut vers VocabDir au premier lancement -
    // individuellement, pour pouvoir ajouter de nouvelles catégories dans
    // une future version sans jamais toucher aux fichiers déjà présents
    // chez l'utilisateur (et donc sans jamais perdre vocab_progress.json).
    public static void SeedDefaults()
    {
        if (!Directory.Exists(Config.VocabDefaultsDir)) return;
        foreach (var src in Directory.GetFiles(Config.VocabDefaultsDir, "*.json"))
        {
            var dest = Path.Combine(Config.VocabDir, Path.GetFileName(src));
            if (File.Exists(dest)) continue;
            File.Copy(src, dest);
        }
    }

    private static Dictionary<string, string> LoadProgress()
    {
        if (!File.Exists(Config.VocabProgressFile)) return new Dictionary<string, string>();
        try
        {
            var json = File.ReadAllText(Config.VocabProgressFile);
            return JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new Dictionary<string, string>();
        }
        catch
        {
            return new Dictionary<string, string>();
        }
    }

    private static void SaveProgress(Dictionary<string, string> progress)
    {
        File.WriteAllText(Config.VocabProgressFile, JsonSerializer.Serialize(progress, Json.Options));
    }

    public static List<VocabWord> LoadAll()
    {
        var progress = LoadProgress();
        var words = new List<VocabWord>();
        if (!Directory.Exists(Config.VocabDir)) return words;

        var files = Directory.GetFiles(Config.VocabDir, "*.json").OrderBy(f => f, StringComparer.Ordinal);
        foreach (var file in files)
        {
            try
            {
                var json = File.ReadAllText(file);
                var entries = JsonSerializer.Deserialize<List<RawVocabEntry>>(json, Json.Options) ?? new List<RawVocabEntry>();
                foreach (var e in entries)
                {
                    if (string.IsNullOrEmpty(e.Id) || string.IsNullOrEmpty(e.Korean)) continue;
                    words.Add(new VocabWord
                    {
                        Id = e.Id,
                        Korean = e.Korean,
                        Meaning = e.Meaning ?? "",
                        ExampleKr = e.ExampleKr ?? "",
                        ExampleFr = e.ExampleFr ?? "",
                        Level = e.Level ?? "",
                        Source = Path.GetFileName(file),
                        Status = progress.GetValueOrDefault(e.Id, "new"),
                    });
                }
            }
            catch
            {
                // fichier corrompu : on l'ignore plutôt que de casser tout le chargement
            }
        }
        return words;
    }

    public static void SetStatus(string id, string status)
    {
        var progress = LoadProgress();
        if (status == "new") progress.Remove(id);
        else progress[id] = status;
        SaveProgress(progress);
    }

    public static VocabStats GetStats()
    {
        var words = LoadAll();
        return new VocabStats
        {
            Total = words.Count,
            New = words.Count(w => w.Status == "new"),
            Review = words.Count(w => w.Status == "review"),
            Mastered = words.Count(w => w.Status == "mastered"),
        };
    }

    private static List<T> Shuffle<T>(List<T> source)
    {
        var a = new List<T>(source);
        var rng = Random.Shared;
        for (var i = a.Count - 1; i > 0; i--)
        {
            var j = rng.Next(i + 1);
            (a[i], a[j]) = (a[j], a[i]);
        }
        return a;
    }

    // Priorité aux mots "à revoir", puis "nouveau" - jamais de mot "maîtrisé"
    // tant qu'il en reste d'autres à réviser.
    public static List<VocabWord> PickChallengeWords(int n)
    {
        var words = LoadAll().Where(w => w.Status != "mastered").ToList();
        var review = Shuffle(words.Where(w => w.Status == "review").ToList());
        var fresh = Shuffle(words.Where(w => w.Status == "new").ToList());
        return review.Concat(fresh).Take(n).ToList();
    }

    // Session d'entraînement à la demande (onglet Vocabulaire) : contrairement
    // au défi du matin (priorité fixe review->nouveau, jamais de maîtrisé), on
    // laisse ici choisir librement quels statuts inclure - y compris rejouer
    // des mots déjà maîtrisés pour entretenir la mémoire.
    public static List<VocabWord> PickPracticeWords(IEnumerable<string>? statuses, int? count)
    {
        var statusList = statuses?.ToList();
        var wanted = new HashSet<string>(statusList != null && statusList.Count > 0 ? statusList : new List<string> { "new", "review", "mastered" });
        var words = Shuffle(LoadAll().Where(w => wanted.Contains(w.Status)).ToList());
        return count.HasValue ? words.Take(count.Value).ToList() : words;
    }

    // -----------------------------------------------------------------
    // Import de listes perso (.txt/.csv/.tsv/.json) - beaucoup de sites de
    // vocabulaire coréen (TOPIK, Anki shared decks...) exportent dans un de
    // ces formats. On accepte plusieurs formes courantes sans imposer un
    // schéma strict.
    // -----------------------------------------------------------------

    private static string Slugify(string s)
    {
        var lower = s.ToLowerInvariant();
        var chars = lower.Select(c => (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') ? c : '_').ToArray();
        var collapsed = System.Text.RegularExpressions.Regex.Replace(new string(chars), "_+", "_").Trim('_');
        return collapsed.Length > 24 ? collapsed[..24] : collapsed;
    }

    // Slugify(korean) est presque toujours vide pour du texte coréen (il ne
    // garde que a-z0-9), donc deux mots coréens différents au même index
    // dans deux imports différents généreraient le même id et se
    // percuteraient silencieusement. On dérive un petit hash déterministe
    // du texte à la place (même algorithme que côté JS : hash de Java/JS
    // avec Math.imul, pour garder des id stables si jamais on partage des
    // fichiers importés entre les deux versions de l'app).
    private static string ShortHash(string s)
    {
        var h = 0;
        foreach (var c in s)
        {
            h = unchecked(31 * h + c);
        }
        var uh = unchecked((uint)h);
        return ToBase36(uh);
    }

    private static string ToBase36(uint value)
    {
        const string digits = "0123456789abcdefghijklmnopqrstuvwxyz";
        if (value == 0) return "0";
        var sb = new System.Text.StringBuilder();
        while (value > 0)
        {
            sb.Insert(0, digits[(int)(value % 36)]);
            value /= 36;
        }
        return sb.ToString();
    }

    private class ImportEntry
    {
        public string Id { get; set; } = "";
        public string Korean { get; set; } = "";
        public string Meaning { get; set; } = "";

        [JsonPropertyName("example_kr")]
        public string ExampleKr { get; set; } = "";

        [JsonPropertyName("example_fr")]
        public string ExampleFr { get; set; } = "";

        public string Level { get; set; } = "import";
    }

    private static List<ImportEntry> NormalizeImportedEntries(List<Dictionary<string, JsonElement>> raw, string sourceName)
    {
        var slug = Slugify(sourceName);
        var out_ = new List<ImportEntry>();
        for (var i = 0; i < raw.Count; i++)
        {
            var item = raw[i];
            var korean = FirstNonEmpty(item, "korean", "front", "term", "word", "question");
            var meaning = FirstNonEmpty(item, "meaning", "back", "definition", "translation", "answer");
            var exampleKr = FirstNonEmpty(item, "example_kr", "example") ?? "";
            var exampleFr = FirstNonEmpty(item, "example_fr", "example_translation") ?? "";
            if (string.IsNullOrEmpty(korean) || string.IsNullOrEmpty(meaning)) continue;
            out_.Add(new ImportEntry
            {
                Id = $"{slug}_{i}_{ShortHash(korean)}",
                Korean = korean.Trim(),
                Meaning = meaning.Trim(),
                ExampleKr = exampleKr.Trim(),
                ExampleFr = exampleFr.Trim(),
            });
        }
        return out_;
    }

    private static string? FirstNonEmpty(Dictionary<string, JsonElement> item, params string[] keys)
    {
        foreach (var k in keys)
        {
            if (item.TryGetValue(k, out var v) && v.ValueKind == JsonValueKind.String)
            {
                var s = v.GetString();
                if (!string.IsNullOrEmpty(s)) return s;
            }
        }
        return null;
    }

    private static List<Dictionary<string, JsonElement>> ParseTextList(string content)
    {
        var lines = content.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None)
            .Select(l => l.Trim())
            .Where(l => l.Length > 0);
        var entries = new List<Dictionary<string, JsonElement>>();
        foreach (var line in lines)
        {
            string[]? parts = null;
            if (line.Contains('\t')) parts = line.Split('\t');
            else if (line.Contains(';')) parts = line.Split(';');
            else if (line.Contains('|')) parts = line.Split('|');
            else if (line.Contains(',')) parts = line.Split(',');
            if (parts == null) continue;

            var dict = new Dictionary<string, JsonElement>();
            var keys = new[] { "korean", "meaning", "example_kr" };
            for (var i = 0; i < parts.Length && i < keys.Length; i++)
            {
                dict[keys[i]] = JsonSerializer.SerializeToElement(parts[i].Trim());
            }
            entries.Add(dict);
        }
        return entries;
    }

    public static ImportResult ImportFile(string filePath)
    {
        var content = File.ReadAllText(filePath);
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        var baseName = Path.GetFileNameWithoutExtension(filePath);

        List<Dictionary<string, JsonElement>> raw;
        if (ext == ".json")
        {
            using var doc = JsonDocument.Parse(content);
            var root = doc.RootElement;
            JsonElement itemsElement;
            if (root.ValueKind == JsonValueKind.Array)
            {
                itemsElement = root;
            }
            else if (root.TryGetProperty("cards", out var cards)) itemsElement = cards;
            else if (root.TryGetProperty("words", out var wordsEl)) itemsElement = wordsEl;
            else if (root.TryGetProperty("entries", out var entriesEl)) itemsElement = entriesEl;
            else itemsElement = default;

            raw = new List<Dictionary<string, JsonElement>>();
            if (itemsElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in itemsElement.EnumerateArray())
                {
                    var dict = new Dictionary<string, JsonElement>();
                    if (item.ValueKind == JsonValueKind.Object)
                    {
                        foreach (var prop in item.EnumerateObject()) dict[prop.Name] = prop.Value;
                    }
                    else if (item.ValueKind == JsonValueKind.Array)
                    {
                        var arr = item.EnumerateArray().ToList();
                        var keys = new[] { "korean", "meaning", "example_kr" };
                        for (var i = 0; i < arr.Count && i < keys.Length; i++) dict[keys[i]] = arr[i];
                    }
                    raw.Add(dict);
                }
            }
        }
        else
        {
            raw = ParseTextList(content);
        }

        var normalized = NormalizeImportedEntries(raw, baseName);
        if (normalized.Count == 0) return new ImportResult { Count = 0 };

        var destFile = Path.Combine(Config.VocabDir, $"custom_{Slugify(baseName)}.json");
        List<ImportEntry> existing = new();
        if (File.Exists(destFile))
        {
            try
            {
                existing = JsonSerializer.Deserialize<List<ImportEntry>>(File.ReadAllText(destFile), Json.Options) ?? new List<ImportEntry>();
            }
            catch
            {
                existing = new List<ImportEntry>();
            }
        }
        var existingIds = new HashSet<string>(existing.Select(e => e.Id));
        var toAdd = normalized.Where(e => !existingIds.Contains(e.Id)).ToList();
        var combined = existing.Concat(toAdd).ToList();
        File.WriteAllText(destFile, JsonSerializer.Serialize(combined, Json.Options));
        return new ImportResult { Count = toAdd.Count, File = Path.GetFileName(destFile) };
    }
}
