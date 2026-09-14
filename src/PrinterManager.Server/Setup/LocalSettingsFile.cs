using System.Text.Json;
using System.Text.Json.Nodes;

namespace PrinterManager.Server.Setup;

/// <summary>
/// Liest und schreibt <c>appsettings.Local.json</c> — die Datei, in der der Server seine
/// erzeugten Geheimnisse und die über die Oberfläche gepflegten Einstellungen ablegt.
/// Sie liegt neben der Anwendung, ist in <c>.gitignore</c> und wird beim Start als
/// Konfigurationsquelle eingebunden.
/// </summary>
public sealed class LocalSettingsFile
{
    public const string FileName = "appsettings.Local.json";

    private static readonly JsonSerializerOptions WriteOptions = new() { WriteIndented = true };

    private readonly JsonObject _root;

    private LocalSettingsFile(string path, JsonObject root)
    {
        Path = path;
        _root = root;
    }

    public string Path { get; }

    public static LocalSettingsFile Load(string contentRootPath)
    {
        var path = System.IO.Path.Combine(contentRootPath, FileName);
        return new LocalSettingsFile(path, ReadRoot(path));
    }

    /// <summary>Liest einen Wert über seinen Konfigurationspfad, z. B. <c>Https:Port</c>.</summary>
    public string? Read(string configPath)
    {
        JsonNode? node = _root;

        foreach (var segment in configPath.Split(':'))
        {
            if (node is not JsonObject obj)
                return null;

            node = obj[segment];
        }

        return node switch
        {
            null => null,
            JsonValue value => value.ToString(),
            _ => node.ToJsonString()
        };
    }

    /// <summary>
    /// Setzt einen Wert. <c>null</c> entfernt den Eintrag, damit wieder der Wert aus
    /// appsettings.json bzw. die Vorgabe greift.
    /// </summary>
    public void Write(string configPath, string? value)
    {
        var segments = configPath.Split(':');
        var node = _root;

        for (var i = 0; i < segments.Length - 1; i++)
        {
            if (node[segments[i]] is not JsonObject child)
            {
                if (value == null)
                    return;

                child = new JsonObject();
                node[segments[i]] = child;
            }

            node = child;
        }

        if (value == null)
        {
            node.Remove(segments[^1]);
        }
        else
        {
            node[segments[^1]] = value;
        }
    }

    /// <summary>Ersetzt eine Liste vollständig (z. B. <c>Cors:AllowedOrigins</c>).</summary>
    public void WriteArray(string configPath, IEnumerable<string> values)
    {
        var segments = configPath.Split(':');
        var node = _root;

        for (var i = 0; i < segments.Length - 1; i++)
        {
            if (node[segments[i]] is not JsonObject child)
            {
                child = new JsonObject();
                node[segments[i]] = child;
            }

            node = child;
        }

        var array = new JsonArray();
        foreach (var value in values)
        {
            array.Add(value);
        }

        node[segments[^1]] = array;
    }

    public IReadOnlyList<string> ReadArray(string configPath)
    {
        JsonNode? node = _root;

        foreach (var segment in configPath.Split(':'))
        {
            if (node is not JsonObject obj)
                return [];

            node = obj[segment];
        }

        if (node is not JsonArray array)
            return [];

        return array
            .Select(item => item?.GetValue<string>())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item!)
            .ToList();
    }

    public bool TrySave()
    {
        try
        {
            File.WriteAllText(Path, _root.ToJsonString(WriteOptions));
            RestrictToOwner(Path);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>Prüft ohne dauerhafte Änderung, ob die Datei beschreibbar ist.</summary>
    public bool IsWritable()
    {
        try
        {
            if (File.Exists(Path))
            {
                using var stream = File.Open(Path, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);
                return true;
            }

            var directory = System.IO.Path.GetDirectoryName(Path);
            return directory != null && Directory.Exists(directory);
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static JsonObject ReadRoot(string path)
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
