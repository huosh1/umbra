using System.Text.Json;
using Umbra.Core;

namespace Umbra.Tests;

public class VocabTests : IDisposable
{
    private readonly string _tempDir;

    public VocabTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "umbra-tests-" + Guid.NewGuid());
        Config.DataDir = _tempDir;
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { }
    }

    private static void SeedWords(object words)
    {
        Directory.CreateDirectory(Config.VocabDir);
        foreach (var f in Directory.GetFiles(Config.VocabDir)) File.Delete(f);
        File.WriteAllText(Path.Combine(Config.VocabDir, "test.json"), JsonSerializer.Serialize(words));
    }

    private static void SeedProgress(object progress)
    {
        File.WriteAllText(Config.VocabProgressFile, JsonSerializer.Serialize(progress));
    }

    [Fact]
    public void PickPracticeWords_FiltersByRequestedStatusesOnly()
    {
        SeedWords(new[]
        {
            new { id = "w1", korean = "하나", meaning = "one" },
            new { id = "w2", korean = "둘", meaning = "two" },
            new { id = "w3", korean = "셋", meaning = "three" },
        });
        SeedProgress(new Dictionary<string, string> { ["w1"] = "mastered", ["w2"] = "review" }); // w3 reste "new" par defaut

        var reviewOnly = Vocab.PickPracticeWords(new[] { "review" }, null);
        Assert.Equal(new[] { "w2" }, reviewOnly.Select(w => w.Id));

        var newAndReview = Vocab.PickPracticeWords(new[] { "new", "review" }, null).Select(w => w.Id).OrderBy(x => x).ToList();
        Assert.Equal(new[] { "w2", "w3" }, newAndReview);
    }

    [Fact]
    public void PickPracticeWords_DefaultsToEveryStatus_WhenNoneSpecified()
    {
        SeedWords(new[] { new { id = "w1", korean = "하나", meaning = "one" } });
        SeedProgress(new Dictionary<string, string>());
        Assert.Single(Vocab.PickPracticeWords(null, null));
    }

    [Fact]
    public void PickPracticeWords_RespectsCountLimit_WithoutErroringWhenExceedingPool()
    {
        SeedWords(new[]
        {
            new { id = "w1", korean = "하나", meaning = "one" },
            new { id = "w2", korean = "둘", meaning = "two" },
        });
        SeedProgress(new Dictionary<string, string>());
        Assert.Single(Vocab.PickPracticeWords(null, 1));
        Assert.Equal(2, Vocab.PickPracticeWords(null, 50).Count);
    }

    [Fact]
    public void PickPracticeWords_CanIncludeMasteredWordsOnDemand()
    {
        SeedWords(new[] { new { id = "w1", korean = "하나", meaning = "one" } });
        SeedProgress(new Dictionary<string, string> { ["w1"] = "mastered" });
        Assert.Equal(new[] { "w1" }, Vocab.PickPracticeWords(new[] { "mastered" }, null).Select(w => w.Id));
    }
}
