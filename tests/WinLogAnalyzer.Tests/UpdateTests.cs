using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using WinLogAnalyzer.Core.Update;
using Xunit;

namespace WinLogAnalyzer.Tests;

public class UpdateTests
{
    private const string Prefix = "https://example.test/releases/download/";
    private static readonly ECDsa Key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
    private static readonly string Pub = Convert.ToBase64String(Key.ExportSubjectPublicKeyInfo());

    private sealed class FakeHandler(string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r, CancellationToken ct)
            => Task.FromResult(new HttpResponseMessage { Content = new StringContent(body) });
    }

    private static string Manifest(string version = "2.0.0", string? url = null, long size = 10,
        string? sha = null, string notes = "n", ECDsa? signer = null, Action<Dictionary<string, object>>? tamper = null)
    {
        url ??= Prefix + "v2/WinLogAnalyzer-Setup-2.0.0.exe";
        sha ??= new string('a', 64);
        var sig = (signer ?? Key).SignData(
            Encoding.UTF8.GetBytes(AppUpdater.SignedMessage(version, url, size, sha, notes)), HashAlgorithmName.SHA256);
        var d = new Dictionary<string, object>
        { ["version"] = version, ["url"] = url, ["size"] = size, ["sha256"] = sha, ["notes"] = notes,
          ["signature"] = Convert.ToBase64String(sig) };
        tamper?.Invoke(d);
        return JsonSerializer.Serialize(d);
    }

    private static AppUpdater Updater(string current = "1.9.1", string body = "")
        => new(new HttpClient(new FakeHandler(body)), Version.Parse(current), Pub, "https://example.test/latest.json", Prefix);

    [Fact]
    public void ValidManifest_IsAccepted()
    {
        var info = Updater().ParseAndVerify(Manifest());
        Assert.Equal(new Version(2, 0, 0, 0), info.Version);
        Assert.Equal(10, info.Size);
    }

    [Fact]
    public void WrongKey_IsRejected()
    {
        using var other = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var ex = Assert.Throws<UpdateException>(() => Updater().ParseAndVerify(Manifest(signer: other)));
        Assert.Contains("Signature", ex.Message);
    }

    [Theory]
    [InlineData("sha256")]
    [InlineData("size")]
    [InlineData("notes")]
    [InlineData("version")]
    [InlineData("url")]
    public void TamperedField_BreaksSignature(string field)
    {
        var json = Manifest(tamper: d => d[field] = field switch
        {
            "size" => 11L,
            "sha256" => new string('b', 64),
            "version" => "9.9.9",
            "url" => Prefix + "v2/Evil.exe",
            _ => "autre",
        });
        Assert.Throws<UpdateException>(() => Updater().ParseAndVerify(json));
    }

    [Theory]
    [InlineData("http://example.test/releases/download/x.exe")]
    [InlineData("https://evil.test/releases/download/x.exe")]
    [InlineData("https://example.test/releases/download/../../x.exe")]
    [InlineData("https://example.test/releases/download/x.zip")]
    [InlineData("https://example.test/releases/download@evil.test/x.exe")]
    public void OffChannelUrl_IsRefusedEvenWhenSigned(string url)
    {
        var ex = Assert.Throws<UpdateException>(() => Updater().ParseAndVerify(Manifest(url: url)));
        Assert.Contains("canal", ex.Message);
    }

    [Fact]
    public void GarbageManifest_IsRejected()
        => Assert.Throws<UpdateException>(() => Updater().ParseAndVerify("{pas du json"));

    [Theory]
    [InlineData("1.9.1", "2.0.0", true)]
    [InlineData("1.10.0", "1.9.1", false)]
    [InlineData("2.0.0", "2.0.0", false)]
    [InlineData("2.0.0.0", "2.0.0", false)] // 3 vs 4 segments : meme version
    public async Task CheckAsync_ComparesVersions(string current, string offered, bool expectUpdate)
    {
        var body = Manifest(version: offered);
        var info = await Updater(current, body).CheckAsync();
        Assert.Equal(expectUpdate, info is not null);
    }

    [Fact]
    public void VerifyFile_DetectsDifferentHashAndSize()
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, "contenu");
            var size = new FileInfo(path).Length;
            var good = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
            var v = new Version(2, 0);
            AppUpdater.VerifyFile(path, new UpdateInfo(v, "u", size, good, ""));
            Assert.Throws<UpdateException>(() => AppUpdater.VerifyFile(path, new UpdateInfo(v, "u", size, new string('0', 64), "")));
            Assert.Throws<UpdateException>(() => AppUpdater.VerifyFile(path, new UpdateInfo(v, "u", size + 1, good, "")));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void ShippedPublicKey_IsAValidP256Key()
    {
        using var k = ECDsa.Create();
        k.ImportSubjectPublicKeyInfo(Convert.FromBase64String(AppUpdater.PublicKeyBase64), out _);
        Assert.Equal(256, k.KeySize);
    }
}
