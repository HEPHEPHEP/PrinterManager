using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace PrinterManager.Server.Setup;

/// <summary>
/// Stellt die Geheimnisse bereit, die der Server zwingend braucht: erst aus der
/// vorhandenen Konfiguration, sonst aus <c>appsettings.Local.json</c>, sonst neu erzeugt
/// und dort abgelegt. Dadurch läuft eine frische Installation ohne vorbereitete
/// Umgebungsvariablen — und die Werte überleben einen Neustart, sodass bereits
/// ausgestellte JWTs gültig bleiben.
/// </summary>
public static class LocalSecrets
{
    public const string FileName = "appsettings.Local.json";
    public const string JwtKeyPath = "Jwt:Key";
    public const string ClientApiKeyPath = "ClientApi:Key";

    /// <param name="Values">
    /// Werte, die der Konfiguration nachgereicht werden müssen. Enthält nur Schlüssel, die
    /// dort gefehlt haben — eine gesetzte Umgebungsvariable behält also Vorrang.
    /// </param>
    /// <param name="GeneratedPaths">In diesem Lauf neu erzeugte Schlüssel.</param>
    /// <param name="Path">Pfad der Datei, oder <c>null</c>, wenn nicht geschrieben werden konnte.</param>
    public sealed record Result(
        IReadOnlyDictionary<string, string?> Values,
        IReadOnlyList<string> GeneratedPaths,
        string? Path)
    {
        public bool AnythingGenerated => GeneratedPaths.Count > 0;
    }

    public static Result Ensure(IConfiguration configuration, string contentRootPath)
    {
        var path = Path.Combine(contentRootPath, FileName);
        var stored = ReadExisting(path);

        var values = new Dictionary<string, string?>();
        var generated = new List<string>();

        // Der JWT-Schlüssel signiert mit HMAC-SHA256; kürzer als 32 Zeichen lehnt die
        // Token-Bibliothek ab.
        Resolve(JwtKeyPath, 64, minimumLength: 32);
        Resolve(ClientApiKeyPath, 32, minimumLength: 16);

        var written = generated.Count == 0 || TryPersist(path, stored);

        return new Result(values, generated, written ? path : null);

        void Resolve(string configPath, int byteCount, int minimumLength)
        {
            // 1. Bereits konfiguriert (appsettings, Umgebungsvariable, ...) — nichts tun.
            if (IsUsable(configuration[configPath], minimumLength))
                return;

            // 2. Aus einem früheren Lauf vorhanden — nachreichen, nicht neu erzeugen.
            var existing = Read(stored, configPath);
            if (IsUsable(existing, minimumLength))
            {
                values[configPath] = existing;
                return;
            }

            // 3. Neu erzeugen und für den nächsten Start festhalten.
            var secret = Convert.ToBase64String(RandomNumberGenerator.GetBytes(byteCount));
            values[configPath] = secret;
            Write(stored, configPath, secret);
            generated.Add(configPath);
        }
    }

    private static bool IsUsable(string? value, int minimumLength) =>
        !string.IsNullOrWhiteSpace(value) && value.Length >= minimumLength;

    private static string? Read(JsonObject root, string configPath)
    {
        JsonNode? node = root;

        foreach (var segment in configPath.Split(':'))
        {
            if (node is not JsonObject obj)
                return null;

            node = obj[segment];
        }

        return node?.GetValue<string>();
    }

    private static void Write(JsonObject root, string configPath, string value)
    {
        var segments = configPath.Split(':');
        var node = root;

        for (var i = 0; i < segments.Length - 1; i++)
        {
            if (node[segments[i]] is not JsonObject child)
            {
                child = new JsonObject();
                node[segments[i]] = child;
            }
            node = child;
        }

        node[segments[^1]] = value;
    }

    private static JsonObject ReadExisting(string path)
    {
        if (!File.Exists(path))
            return new JsonObject();

        try
        {
            return JsonNode.Parse(File.ReadAllText(path)) as JsonObject ?? new JsonObject();
        }
        catch (Exception)
        {
            // Kaputte oder unlesbare Datei: neu aufbauen statt den Start zu verhindern.
            return new JsonObject();
        }
    }

    private static bool TryPersist(string path, JsonObject root)
    {
        try
        {
            File.WriteAllText(path, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
            RestrictToOwner(path);
            return true;
        }
        catch (Exception)
        {
            // Nicht schreibbares Verzeichnis (z. B. Read-only-Container): der Aufrufer
            // warnt, die erzeugten Werte gelten dann nur für diesen Prozess.
            return false;
        }
    }

    /// <summary>Entzieht auf Unix allen außer dem Besitzer die Rechte an der Datei.</summary>
    public static void RestrictToOwner(string path)
    {
        if (OperatingSystem.IsWindows())
            return;

        try
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
        catch (Exception)
        {
            // Dateisysteme ohne Unix-Rechte ignorieren.
        }
    }
}
