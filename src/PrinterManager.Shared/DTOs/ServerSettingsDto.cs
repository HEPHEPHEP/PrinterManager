namespace PrinterManager.Shared.DTOs;

/// <summary>
/// Server-Einstellungen, die in <c>appsettings.Local.json</c> liegen. Sie werden beim
/// Start gelesen — Änderungen greifen erst nach einem Neustart.
/// </summary>
public class ServerSettingsDto
{
    public HttpsSettingsDto Https { get; set; } = new();
    public ClientApiSettingsDto ClientApi { get; set; } = new();
    public JwtSettingsDto Jwt { get; set; } = new();

    /// <summary>
    /// Erlaubte Browser-Herkünfte. Nur nötig, wenn ein Browser direkt auf die API
    /// zugreift — die Blazor-Oberfläche ruft serverseitig auf.
    /// </summary>
    public List<string> CorsAllowedOrigins { get; set; } = new();
}

public class HttpsSettingsDto
{
    public bool Enabled { get; set; } = true;
    public int Port { get; set; } = 5443;

    /// <summary>
    /// Leitet HTTP-Anfragen auf HTTPS um und aktiviert HSTS. Erst einschalten, wenn ein
    /// vertrauenswürdiges Zertifikat eingerichtet ist.
    /// </summary>
    public bool RedirectToHttps { get; set; }

    public string? CertificateThumbprint { get; set; }
    public string? CertificateSubject { get; set; }
    public string? CertificatePath { get; set; }

    /// <summary>Nur lesend: ob ein Passwort hinterlegt ist. Das Passwort selbst wird nie ausgeliefert.</summary>
    public bool HasCertificatePassword { get; set; }

    /// <summary>Nur schreibend: leer lassen, um das vorhandene Passwort beizubehalten.</summary>
    public string? NewCertificatePassword { get; set; }

    /// <summary>Nur schreibend: entfernt ein hinterlegtes Zertifikatspasswort.</summary>
    public bool ClearCertificatePassword { get; set; }
}

public class ClientApiSettingsDto
{
    /// <summary>None, ApiKey oder Windows.</summary>
    public string Authentication { get; set; } = "None";

    /// <summary>Nur lesend: ob ein gemeinsamer Schlüssel hinterlegt ist.</summary>
    public bool HasKey { get; set; }

    /// <summary>Nur schreibend: erzeugt einen neuen Schlüssel. Alle Clients müssen dann angepasst werden.</summary>
    public bool RegenerateKey { get; set; }
}

public class JwtSettingsDto
{
    public string Issuer { get; set; } = "PrinterManager";
    public string Audience { get; set; } = "PrinterManager";

    /// <summary>Gültigkeitsdauer ausgestellter Tokens in Stunden.</summary>
    public int TokenLifetimeHours { get; set; } = 8;

    /// <summary>Nur schreibend: erzeugt einen neuen Signaturschlüssel. Meldet alle Benutzer ab.</summary>
    public bool RegenerateKey { get; set; }
}

/// <summary>Laufzeitzustand des Servers — rein informativ, nicht änderbar.</summary>
public class ServerStatusDto
{
    public string Environment { get; set; } = string.Empty;
    public string SettingsFilePath { get; set; } = string.Empty;
    public bool SettingsFileWritable { get; set; }
    public string DatabaseConnection { get; set; } = string.Empty;

    public bool HttpsActive { get; set; }
    public int HttpsPort { get; set; }
    public string CertificateSource { get; set; } = string.Empty;
    public string? CertificateThumbprint { get; set; }
    public string? CertificateSubject { get; set; }
    public DateTime? CertificateExpiresAt { get; set; }
    public bool CertificateIsSelfSigned { get; set; }

    /// <summary>Der beim Start tatsächlich aktive Client-Authentifizierungsmodus.</summary>
    public string ActiveClientAuthentication { get; set; } = string.Empty;

    /// <summary>
    /// Einstellungen, die von einer Umgebungsvariable oder appsettings.Production.json
    /// überschrieben werden. Änderungen in der Oberfläche bleiben dort wirkungslos.
    /// </summary>
    public List<string> OverriddenKeys { get; set; } = new();

    /// <summary>Warnungen aus der HTTPS-Einrichtung beim Start.</summary>
    public List<string> Warnings { get; set; } = new();
}

public class SaveSettingsResultDto
{
    public bool RestartRequired { get; set; }
    public List<string> ChangedKeys { get; set; } = new();

    /// <summary>Neu erzeugter Client-Schlüssel — nur direkt nach dem Erzeugen befüllt.</summary>
    public string? NewClientApiKey { get; set; }
}

public class ClientApiKeyDto
{
    public string? Key { get; set; }
}
