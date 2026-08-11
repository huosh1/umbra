using System.Diagnostics;
using System.IO;
using System.Text.Json;
using Microsoft.Win32;
using Umbra.Core;

namespace Umbra.App;

internal static class BrowserIntegration
{
    public const string HostName = "com.umbra.browser_blocker";
    public const string ExtensionId = "ijgalicomdmmcjecigefpchbdeiadnld";
    public const string StoreUrl = "https://chromewebstore.google.com/detail/" + ExtensionId;

    private static readonly string[] VendorKeys =
    [
        @"Software\Google\Chrome\NativeMessagingHosts",
        @"Software\Vivaldi\NativeMessagingHosts",
        @"Software\Microsoft\Edge\NativeMessagingHosts",
        @"Software\BraveSoftware\Brave-Browser\NativeMessagingHosts",
    ];

    public static bool RegisterNativeHost()
    {
        var hostExe = FindHostExecutable();
        if (hostExe is null) return false;

        try
        {
            var manifestPath = Path.Combine(Config.DataDir, "browser-native-host.json");
            var manifest = new
            {
                name = HostName,
                description = "Umbra browser blocking host",
                path = hostExe,
                type = "stdio",
                allowed_origins = new[] { $"chrome-extension://{ExtensionId}/" },
            };
            File.WriteAllText(manifestPath, JsonSerializer.Serialize(manifest, Json.Options));

            foreach (var vendorKey in VendorKeys)
            {
                using var key = Registry.CurrentUser.CreateSubKey($@"{vendorKey}\{HostName}");
                key?.SetValue(null, manifestPath);
            }
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static void OpenStorePage() => Process.Start(new ProcessStartInfo
    {
        FileName = StoreUrl,
        UseShellExecute = true,
    });

    private static string? FindHostExecutable()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "browser-host", "Umbra.BrowserHost.exe"),
            Path.Combine(AppContext.BaseDirectory, "Umbra.BrowserHost.exe"),
        };
        return candidates.FirstOrDefault(File.Exists);
    }
}
