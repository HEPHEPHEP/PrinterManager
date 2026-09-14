using System.Security.Cryptography;

namespace PrinterManager.Server.Setup;

/// <summary>
/// Stellt die Geheimnisse bereit, die der Server zwingend braucht: erst aus der
/// vorhandenen Konfiguration, sonst neu erzeugt und in <c>appsettings.Local.json</c>
/// abgelegt. Dadurch läuft eine frische Installation ohne vorbereitete
/// Umgebungsvariablen — und die Werte überleben einen Neustart, sodass bereits
/// ausgestellte JWTs gültig bleiben.
/// </summary>
public static class LocalSecrets
{
    public const string JwtKeyPath = "Jwt:Key";
    public const string ClientApiKeyPath = "ClientApi:Key";

    /// <summary>Der JWT-Schlüssel signiert mit HMAC-SHA256; kürzer als 32 Zeichen lehnt die Bibliothek ab.</summary>
    public const int MinimumJwtKeyLength = 32;

    /// <param name="Values">
    /// Werte, die der Konfiguration für diesen Lauf nachgereicht werden müssen. Enthält nur
    /// neu erzeugte Schlüssel — eine gesetzte Umgebungsvariable behält Vorrang.
    /// </param>
    public sealed record Result(
        IReadOnlyDictionary<string, string?> Values,
        IReadOnlyList<string> GeneratedPaths,
        string? Path)
    {
        public bool AnythingGenerated => GeneratedPaths.Count > 0;
    }

    public static Result Ensure(IConfiguration configuration, string contentRootPath)
    {
        var file = LocalSettingsFile.Load(contentRootPath);
        var values = new Dictionary<string, string?>();
        var generated = new List<string>();

        Resolve(JwtKeyPath, 64, MinimumJwtKeyLength);
        Resolve(ClientApiKeyPath, 32, minimumLength: 16);

        var written = generated.Count == 0 || file.TrySave();

        return new Result(values, generated, written ? file.Path : null);

        void Resolve(string configPath, int byteCount, int minimumLength)
        {
            // Bereits vorhanden — aus appsettings, appsettings.Local.json oder einer
            // Umgebungsvariable. Dann nichts erzeugen.
            var existing = configuration[configPath];
            if (!string.IsNullOrWhiteSpace(existing) && existing.Length >= minimumLength)
                return;

            var secret = Generate(byteCount);
            values[configPath] = secret;
            file.Write(configPath, secret);
            generated.Add(configPath);
        }
    }

    public static string Generate(int byteCount) =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(byteCount));
}
