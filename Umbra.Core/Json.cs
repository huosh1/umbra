using System.Text.Json;

namespace Umbra.Core;

// Options JSON partagées - camelCase pour matcher le format des fichiers de
// données existants (compatibles avec ce que l'app Electron écrit).
public static class Json
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };
}
