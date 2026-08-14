using System.Text.Json;

namespace Umbra.Core;

public class SessionTaskItem
{
    public string Id { get; set; } = "";
    public string Text { get; set; } = "";
    public bool Done { get; set; }
}

public class SessionTasksData
{
    public List<SessionTaskItem> Tasks { get; set; } = new();
}

// Petite checklist optionnelle affichée pendant les sessions de focus
// (Réglages > Session tasks) - volontairement pas liée à une session ou
// une date précise : les tâches restent d'une session à l'autre jusqu'à
// être cochées/supprimées, comme un pense-bête plutôt qu'un gestionnaire
// de projet.
public static class SessionTasks
{
    public static SessionTasksData Load()
    {
        if (!File.Exists(Config.SessionTasksFile)) return new SessionTasksData();
        try
        {
            var json = File.ReadAllText(Config.SessionTasksFile);
            return JsonSerializer.Deserialize<SessionTasksData>(json, Json.Options) ?? new SessionTasksData();
        }
        catch
        {
            return new SessionTasksData();
        }
    }

    public static void Save(SessionTasksData data)
    {
        File.WriteAllText(Config.SessionTasksFile, JsonSerializer.Serialize(data, Json.Options));
    }
}
