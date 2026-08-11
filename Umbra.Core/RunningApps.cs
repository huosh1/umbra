using System.Diagnostics;

namespace Umbra.Core;

// Liste les applications actuellement ouvertes (fenêtre visible) pour
// permettre de les ajouter au blocage en un clic - évite d'avoir à taper le
// nom exact du .exe à la main.
public static class RunningApps
{
    public static List<string> GetVisibleAppNames()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var names = new List<string>();
        foreach (var p in Process.GetProcesses())
        {
            try
            {
                if (p.MainWindowHandle == IntPtr.Zero) continue;
                if (string.IsNullOrWhiteSpace(p.MainWindowTitle)) continue;
                var exeName = p.ProcessName + ".exe";
                if (Config.ProtectedProcesses.Contains(exeName)) continue;
                if (seen.Add(exeName)) names.Add(exeName);
            }
            catch
            {
                // process disparu entre l'énumération et l'accès à ses propriétés
            }
            finally
            {
                p.Dispose();
            }
        }
        names.Sort(StringComparer.OrdinalIgnoreCase);
        return names;
    }
}
