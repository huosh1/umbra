using Umbra.Core;

namespace Umbra.Tests;

public class UpdaterTests
{
    [Theory]
    [InlineData("1.0.3", "1.0.2", 1)]
    [InlineData("v2.0.0", "1.9.9", 1)]
    [InlineData("1.0.0", "1.0", 0)]
    [InlineData("1.0.2", "1.0.3", -1)]
    [InlineData("release-1.10.0", "1.9.9", 1)]
    public void CompareVersions_UsesNumericComponents(string left, string right, int expectedSign)
    {
        Assert.Equal(expectedSign, Math.Sign(Updater.CompareVersions(left, right)));
    }

    [Fact]
    public void TryReadExpectedSha256_SelectsTheNamedInstaller()
    {
        const string wantedHash = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
        var contents = $"{'a',64}  Umbra-Extension.zip\r\n{wantedHash}  Umbra-Setup-1.0.3-x64.exe\r\n";

        var found = Updater.TryReadExpectedSha256(contents, "Umbra-Setup-1.0.3-x64.exe", out var actual);

        Assert.True(found);
        Assert.Equal(wantedHash, actual);
    }

    [Theory]
    [InlineData("https://github.com/zixload/umbra/releases/download/v1.0.3/Umbra.exe", true)]
    [InlineData("https://release-assets.githubusercontent.com/file", true)]
    [InlineData("http://github.com/zixload/umbra/file", false)]
    [InlineData("https://github.com.example.test/file", false)]
    [InlineData("https://example.test/file", false)]
    public void IsTrustedGitHubUrl_AllowsOnlyHttpsGitHubHosts(string url, bool expected)
    {
        Assert.Equal(expected, Updater.IsTrustedGitHubUrl(url));
    }

    [Fact]
    public async Task CompletedDownload_ReleasesTemporaryFileBeforePromotion()
    {
        var directory = Path.Combine(Path.GetTempPath(), "umbra-updater-test-" + Guid.NewGuid());
        Directory.CreateDirectory(directory);
        var temporaryPath = Path.Combine(directory, "Umbra-Setup.exe.download");
        var destinationPath = Path.Combine(directory, "Umbra-Setup.exe");
        var payload = "verified installer payload"u8.ToArray();

        try
        {
            await using var source = new MemoryStream(payload);
            var hash = await Updater.WriteVerifiedDownloadAsync(source, temporaryPath, payload.Length);

            Assert.Equal(
                Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(payload)).ToLowerInvariant(),
                hash);
            File.Move(temporaryPath, destinationPath);
            Assert.Equal(payload, await File.ReadAllBytesAsync(destinationPath));
        }
        finally
        {
            try { Directory.Delete(directory, recursive: true); } catch { }
        }
    }
}
