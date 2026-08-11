using Umbra.Core;

namespace Umbra.Tests;

// Redirige Config.HostsPath vers un fichier temporaire par test - jamais
// question d'écrire dans le vrai fichier hosts système pendant les tests.
public class BlockerTests : IDisposable
{
    private readonly string _tempDataDir;
    private readonly string _hostsPath;

    public BlockerTests()
    {
        _tempDataDir = Path.Combine(Path.GetTempPath(), "umbra-tests-" + Guid.NewGuid());
        Config.DataDir = _tempDataDir;
        _hostsPath = Path.Combine(_tempDataDir, "hosts");
        Config.HostsPath = _hostsPath;
    }

    public void Dispose()
    {
        Config.HostsPath = @"C:\Windows\System32\drivers\etc\hosts";
        try { Directory.Delete(_tempDataDir, recursive: true); } catch { }
    }

    private string ReadHosts() => File.ReadAllText(_hostsPath);
    private void ResetHosts(string content = "127.0.0.1 localhost\n") => File.WriteAllText(_hostsPath, content);

    [Fact]
    public void ApplySiteBlock_AddsEntries_RemoveSiteBlock_CleansThem_PreservesRest()
    {
        ResetHosts();
        Blocker.ApplySiteBlock(new[] { "example-test-site.com" });
        Assert.Contains("example-test-site.com", ReadHosts());

        Blocker.RemoveSiteBlock();
        var after = ReadHosts();
        Assert.DoesNotContain("example-test-site.com", after);
        Assert.Contains("127.0.0.1 localhost", after);
    }

    [Fact]
    public void RemoveSiteBlock_IsTrueNoOp_WhenNothingToRemove_NoDiskWrite()
    {
        ResetHosts();
        var before = File.GetLastWriteTimeUtc(_hostsPath);
        var original = ReadHosts();
        Blocker.RemoveSiteBlock();
        Assert.Equal(original, ReadHosts());
        Assert.Equal(before, File.GetLastWriteTimeUtc(_hostsPath));
    }

    // Reproduit le scénario réel du bug corrigé côté Electron : un watchdog
    // qui redémarre doit pouvoir nettoyer un blocage "orphelin" qu'il n'a
    // jamais lui-même appliqué dans CE process. RemoveSiteBlock() doit
    // fonctionner par simple inspection du fichier hosts, jamais par un état
    // mémoire supposé.
    [Fact]
    public void RemoveSiteBlock_CleansUpOrphanedBlock_EvenWithoutPriorApplyInThisProcess()
    {
        ResetHosts("127.0.0.1 localhost\n\n# --- UMBRANATIVE BLOCK START ---\n127.0.0.1 twitch.tv\n127.0.0.1 www.twitch.tv\n# --- UMBRANATIVE BLOCK END ---\n");
        Assert.Contains("twitch.tv", ReadHosts());

        Blocker.RemoveSiteBlock(); // aucun ApplySiteBlock() precedent dans ce test

        var after = ReadHosts();
        Assert.DoesNotContain("twitch.tv", after);
        Assert.Contains("127.0.0.1 localhost", after);
    }

    [Fact]
    public void ApplySiteBlock_ReplacingExistingBlock_DoesNotDuplicateOriginalContent()
    {
        ResetHosts();
        Blocker.ApplySiteBlock(new[] { "site-a.com" });
        Blocker.ApplySiteBlock(new[] { "site-b.com" });
        var content = ReadHosts();
        Assert.DoesNotContain("site-a.com", content);
        Assert.Contains("site-b.com", content);
        // Le contenu original ne doit apparaitre qu'une fois, pas s'accumuler a chaque apply
        var occurrences = content.Split("127.0.0.1 localhost").Length - 1;
        Assert.Equal(1, occurrences);
    }
}
