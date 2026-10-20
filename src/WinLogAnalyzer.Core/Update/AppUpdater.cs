using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WinLogAnalyzer.Core.Update;

/// <summary>Mise a jour refusee ou impossible (message en francais, affichable tel quel).</summary>
public sealed class UpdateException(string message) : Exception(message);

/// <summary>Version proposee, apres verification complete de la signature du manifeste.</summary>
public sealed record UpdateInfo(Version Version, string Url, long Size, string Sha256, string Notes);

/// <summary>
/// Mise a jour automatique : manifeste latest.json signe (ECDSA P-256, cle publique embarquee),
/// controle de la taille et du SHA-256 de l'installateur Inno, URL contrainte au canal GitHub.
/// ECDSA P-256 : Ed25519 n'existe pas dans .NET 8, ECDSA est natif (aucune dependance NuGet).
/// </summary>
public sealed class AppUpdater
{
    /// <summary>Cle publique (SubjectPublicKeyInfo, base64). Cle privee hors depot.</summary>
    public const string PublicKeyBase64 =
        "MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAEOEOIPgLgXgBZ4mER0BtAomXua8XWO50a1RSlIUabpcWHbcQATvuPqmhMsaud6ZIq40Py3R1NfZsgkTeXlphKDg==";

    public const string ReleasesBase = "https://github.com/Heiphaistos/Log-Analyzer-AppWindows/releases/";
    public const string ManifestUrl = ReleasesBase + "latest/download/latest.json";
    public const string ReleasePageUrl = ReleasesBase + "latest";

    private const long MaxManifestBytes = 64 * 1024;
    private const long MaxSetupBytes = 300L * 1024 * 1024;

    private readonly HttpClient _http;
    private readonly Version _current;
    private readonly string _publicKey;
    private readonly string _manifestUrl;
    private readonly string _urlPrefix;

    public AppUpdater(HttpClient http, Version current, string publicKey = PublicKeyBase64,
                      string manifestUrl = ManifestUrl, string urlPrefix = ReleasesBase + "download/")
    {
        _http = http;
        _current = Norm(current);
        _publicKey = publicKey;
        _manifestUrl = manifestUrl;
        _urlPrefix = urlPrefix;
    }

    private sealed class Manifest
    {
        [JsonPropertyName("version")] public string? Version { get; set; }
        [JsonPropertyName("url")] public string? Url { get; set; }
        [JsonPropertyName("size")] public long Size { get; set; }
        [JsonPropertyName("sha256")] public string? Sha256 { get; set; }
        [JsonPropertyName("notes")] public string? Notes { get; set; }
        [JsonPropertyName("signature")] public string? Signature { get; set; }
    }

    // 1.10.0 == 1.10.0.0 (sinon Revision -1 < 0 fausse la comparaison).
    private static Version Norm(Version v) => new(v.Major, v.Minor, Math.Max(v.Build, 0), Math.Max(v.Revision, 0));

    /// <summary>Message signe. DOIT rester identique a installer/publier_manifeste.py.</summary>
    public static string SignedMessage(string version, string url, long size, string sha256, string notes)
        => $"winlog-update-v1\n{version}\n{url}\n{size}\n{sha256}\n{notes}";

    /// <summary>Verifie signature + champs + canal. Leve UpdateException sinon.</summary>
    public UpdateInfo ParseAndVerify(string json)
    {
        Manifest? m;
        try { m = JsonSerializer.Deserialize<Manifest>(json); }
        catch (JsonException) { throw new UpdateException("Manifeste de mise a jour illisible."); }
        if (m is null || m.Version is null || m.Url is null || m.Sha256 is null || m.Signature is null
            || m.Size <= 0 || m.Size > MaxSetupBytes)
            throw new UpdateException("Manifeste de mise a jour incomplet.");

        var notes = m.Notes ?? "";
        var sha = m.Sha256.ToLowerInvariant();
        bool sigOk;
        try
        {
            using var key = ECDsa.Create();
            key.ImportSubjectPublicKeyInfo(Convert.FromBase64String(_publicKey), out _);
            sigOk = key.VerifyData(Encoding.UTF8.GetBytes(SignedMessage(m.Version, m.Url, m.Size, sha, notes)),
                                   Convert.FromBase64String(m.Signature), HashAlgorithmName.SHA256);
        }
        catch (Exception ex) when (ex is FormatException or CryptographicException)
        {
            sigOk = false;
        }
        if (!sigOk) throw new UpdateException("Signature du manifeste invalide : mise a jour refusee.");

        if (!Version.TryParse(m.Version, out var version))
            throw new UpdateException("Numero de version invalide dans le manifeste.");
        if (sha.Length != 64 || !sha.All(Uri.IsHexDigit))
            throw new UpdateException("Empreinte SHA-256 invalide dans le manifeste.");
        // Canal : https, sous le prefixe des releases du depot, sans astuce de chemin/identifiant.
        if (!m.Url.StartsWith(_urlPrefix, StringComparison.Ordinal) || m.Url.Contains("..")
            || m.Url.Contains('@') || m.Url.Contains('\\') || !m.Url.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            throw new UpdateException("URL de telechargement hors du canal officiel : mise a jour refusee.");

        return new UpdateInfo(Norm(version), m.Url, m.Size, sha, notes);
    }

    /// <summary>Retourne la mise a jour si elle est plus recente que la version courante, sinon null.</summary>
    public async Task<UpdateInfo?> CheckAsync(CancellationToken ct = default)
    {
        string json;
        try
        {
            using var resp = await _http.GetAsync(_manifestUrl, HttpCompletionOption.ResponseHeadersRead, ct);
            resp.EnsureSuccessStatusCode();
            await using var s = await resp.Content.ReadAsStreamAsync(ct);
            using var ms = new MemoryStream();
            var buf = new byte[8192];
            int n;
            while ((n = await s.ReadAsync(buf, ct)) > 0)
            {
                ms.Write(buf, 0, n);
                if (ms.Length > MaxManifestBytes) throw new UpdateException("Manifeste de mise a jour trop volumineux.");
            }
            json = Encoding.UTF8.GetString(ms.ToArray());
        }
        catch (HttpRequestException ex)
        {
            throw new UpdateException($"Serveur de mises a jour injoignable : {ex.Message}");
        }
        var info = ParseAndVerify(json);
        return info.Version > _current ? info : null;
    }

    /// <summary>Telecharge l'installateur, controle taille + SHA-256, retourne son chemin.</summary>
    public async Task<string> DownloadAsync(UpdateInfo info, IProgress<double>? progress = null, CancellationToken ct = default)
    {
        var dir = Path.Combine(Path.GetTempPath(), "WinLogAnalyzer-update");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, $"WinLogAnalyzer-Setup-{info.Version}.exe");
        try
        {
            using var resp = await _http.GetAsync(info.Url, HttpCompletionOption.ResponseHeadersRead, ct);
            resp.EnsureSuccessStatusCode();
            await using (var src = await resp.Content.ReadAsStreamAsync(ct))
            await using (var dst = File.Create(path))
            {
                var buf = new byte[81920];
                long total = 0;
                int n;
                while ((n = await src.ReadAsync(buf, ct)) > 0)
                {
                    total += n;
                    if (total > info.Size) throw new UpdateException("Fichier telecharge plus gros que prevu : mise a jour refusee.");
                    await dst.WriteAsync(buf.AsMemory(0, n), ct);
                    progress?.Report((double)total / info.Size);
                }
            }
            VerifyFile(path, info);
            return path;
        }
        catch (HttpRequestException ex)
        {
            Delete(path);
            throw new UpdateException($"Telechargement impossible : {ex.Message}");
        }
        catch
        {
            Delete(path);
            throw;
        }
    }

    /// <summary>Controle taille puis SHA-256 ; leve UpdateException si different.</summary>
    public static void VerifyFile(string path, UpdateInfo info)
    {
        if (new FileInfo(path).Length != info.Size)
            throw new UpdateException("Taille du fichier telecharge incorrecte : mise a jour refusee.");
        using var f = File.OpenRead(path);
        var hash = Convert.ToHexString(SHA256.HashData(f)).ToLowerInvariant();
        if (hash != info.Sha256)
            throw new UpdateException("Empreinte SHA-256 incorrecte : fichier corrompu ou altere, mise a jour refusee.");
    }

    /// <summary>True si l'exe courant a ete pose par l'installateur Inno (unins000.exe a cote).</summary>
    public static bool IsInstalledCopy()
        => Environment.ProcessPath is { } p && File.Exists(Path.Combine(Path.GetDirectoryName(p)!, "unins000.exe"));

    /// <summary>Lance l'installateur en silence (l'appelant doit ensuite quitter l'application).</summary>
    public static void LaunchInstaller(string setupPath)
        => System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(setupPath,
            "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /CLOSEAPPLICATIONS /RESTARTAPPLICATIONS")
        { UseShellExecute = true }); // UseShellExecute : l'installateur demande son elevation (UAC)

    private static void Delete(string path)
    {
        try { File.Delete(path); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }
}
