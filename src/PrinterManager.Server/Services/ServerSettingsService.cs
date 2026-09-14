using PrinterManager.Server.Security;
using PrinterManager.Server.Setup;
using PrinterManager.Shared.DTOs;

namespace PrinterManager.Server.Services;

public interface IServerSettingsService
{
    ServerSettingsDto Get();
    ServerStatusDto GetStatus();
    SaveSettingsResultDto Save(ServerSettingsDto dto);
    string? GetClientApiKey();
}

/// <summary>
/// Liest und schreibt die Einstellungen, die vor dem Aufbau des Hosts gebraucht werden
/// und deshalb in <c>appsettings.Local.json</c> liegen. Änderungen greifen erst nach
/// einem Neustart — das meldet <see cref="SaveSettingsResultDto.RestartRequired"/>.
/// </summary>
public class ServerSettingsService : IServerSettingsService
{
    private const string HttpsEnabled = "Https:Enabled";
    private const string HttpsPort = "Https:Port";
    private const string HttpsRedirect = "Https:RedirectToHttps";
    private const string HttpsThumbprint = "Https:CertificateThumbprint";
    private const string HttpsSubject = "Https:CertificateSubject";
    private const string HttpsPath = "Https:CertificatePath";
    private const string HttpsPassword = "Https:CertificatePassword";
    private const string CorsOrigins = "Cors:AllowedOrigins";
    private const string JwtIssuer = "Jwt:Issuer";
    private const string JwtAudience = "Jwt:Audience";
    private const string JwtLifetime = "Jwt:TokenLifetimeHours";

    /// <summary>Einstellungen, deren Herkunft in der Oberfläche angezeigt wird.</summary>
    private static readonly string[] TrackedKeys =
    [
        HttpsEnabled, HttpsPort, HttpsRedirect, HttpsThumbprint, HttpsSubject, HttpsPath,
        ClientAuthenticationOptions.ModeKey, ClientAuthenticationOptions.ApiKeyKey,
        JwtIssuer, JwtAudience, JwtLifetime
    ];

    private readonly IConfiguration _configuration;
    private readonly IHostEnvironment _environment;
    private readonly HttpsSetupResult _https;
    private readonly ClientAuthenticationSettings _clientAuthentication;
    private readonly ILogger<ServerSettingsService> _logger;

    public ServerSettingsService(
        IConfiguration configuration,
        IHostEnvironment environment,
        HttpsSetupResult https,
        ClientAuthenticationSettings clientAuthentication,
        ILogger<ServerSettingsService> logger)
    {
        _configuration = configuration;
        _environment = environment;
        _https = https;
        _clientAuthentication = clientAuthentication;
        _logger = logger;
    }

    public ServerSettingsDto Get()
    {
        return new ServerSettingsDto
        {
            Https = new HttpsSettingsDto
            {
                Enabled = _configuration.GetValue(HttpsEnabled, true),
                Port = _configuration.GetValue(HttpsPort, HttpsSetup.DefaultPort),
                RedirectToHttps = _configuration.GetValue(HttpsRedirect, false),
                CertificateThumbprint = _configuration[HttpsThumbprint],
                CertificateSubject = _configuration[HttpsSubject],
                CertificatePath = _configuration[HttpsPath],
                HasCertificatePassword = !string.IsNullOrEmpty(_configuration[HttpsPassword])
            },
            ClientApi = new ClientApiSettingsDto
            {
                Authentication = (_configuration[ClientAuthenticationOptions.ModeKey]
                    ?? nameof(ClientAuthenticationMode.None)),
                HasKey = !string.IsNullOrEmpty(_configuration[ClientAuthenticationOptions.ApiKeyKey])
            },
            Jwt = new JwtSettingsDto
            {
                Issuer = _configuration[JwtIssuer] ?? "PrinterManager",
                Audience = _configuration[JwtAudience] ?? "PrinterManager",
                TokenLifetimeHours = _configuration.GetValue(JwtLifetime, AuthenticationService.DefaultTokenLifetimeHours)
            },
            CorsAllowedOrigins = _configuration.GetSection(CorsOrigins).Get<string[]>()?.ToList() ?? []
        };
    }

    public ServerStatusDto GetStatus()
    {
        var file = LocalSettingsFile.Load(_environment.ContentRootPath);

        return new ServerStatusDto
        {
            Environment = _environment.EnvironmentName,
            SettingsFilePath = file.Path,
            SettingsFileWritable = file.IsWritable(),
            DatabaseConnection = _configuration.GetConnectionString("DefaultConnection") ?? "—",
            HttpsActive = _https.Enabled,
            HttpsPort = _https.Port,
            CertificateSource = _https.Source,
            CertificateThumbprint = _https.Thumbprint,
            CertificateSubject = _https.Subject,
            CertificateExpiresAt = _https.ExpiresAt,
            CertificateIsSelfSigned = _https.IsSelfSigned,
            ActiveClientAuthentication = _clientAuthentication.Mode.ToString(),
            OverriddenKeys = FindOverriddenKeys(file),
            Warnings = _https.Warnings.ToList()
        };
    }

    public string? GetClientApiKey() => _configuration[ClientAuthenticationOptions.ApiKeyKey];

    public SaveSettingsResultDto Save(ServerSettingsDto dto)
    {
        Validate(dto);

        var file = LocalSettingsFile.Load(_environment.ContentRootPath);
        var changed = new List<string>();
        var result = new SaveSettingsResultDto();

        Set(HttpsEnabled, dto.Https.Enabled ? "true" : "false");
        Set(HttpsPort, dto.Https.Port.ToString());
        Set(HttpsRedirect, dto.Https.RedirectToHttps ? "true" : "false");
        Set(HttpsThumbprint, Trim(dto.Https.CertificateThumbprint));
        Set(HttpsSubject, Trim(dto.Https.CertificateSubject));
        Set(HttpsPath, Trim(dto.Https.CertificatePath));

        // Passwörter: leeres Feld bedeutet "unverändert", nicht "löschen".
        if (dto.Https.ClearCertificatePassword)
        {
            Set(HttpsPassword, null);
        }
        else if (!string.IsNullOrEmpty(dto.Https.NewCertificatePassword))
        {
            Set(HttpsPassword, dto.Https.NewCertificatePassword);
        }

        Set(ClientAuthenticationOptions.ModeKey, dto.ClientApi.Authentication);

        if (dto.ClientApi.RegenerateKey)
        {
            var key = LocalSecrets.Generate(32);
            Set(ClientAuthenticationOptions.ApiKeyKey, key);
            result.NewClientApiKey = key;
        }

        Set(JwtIssuer, Trim(dto.Jwt.Issuer));
        Set(JwtAudience, Trim(dto.Jwt.Audience));
        Set(JwtLifetime, dto.Jwt.TokenLifetimeHours.ToString());

        if (dto.Jwt.RegenerateKey)
        {
            Set(LocalSecrets.JwtKeyPath, LocalSecrets.Generate(64));
        }

        var origins = dto.CorsAllowedOrigins
            .Select(o => o.Trim().TrimEnd('/'))
            .Where(o => o.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (!origins.SequenceEqual(file.ReadArray(CorsOrigins), StringComparer.OrdinalIgnoreCase))
        {
            file.WriteArray(CorsOrigins, origins);
            changed.Add(CorsOrigins);
        }

        if (changed.Count == 0)
        {
            return result;
        }

        if (!file.TrySave())
        {
            throw new InvalidOperationException(
                $"{file.Path} konnte nicht geschrieben werden. Bitte Schreibrechte prüfen.");
        }

        _logger.LogInformation("Server-Einstellungen geändert: {Keys}", string.Join(", ", changed));

        result.ChangedKeys = changed;
        result.RestartRequired = true;
        return result;

        void Set(string key, string? value)
        {
            if (file.Read(key) == value)
                return;

            file.Write(key, value);
            changed.Add(key);
        }
    }

    private static string? Trim(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }

    private static void Validate(ServerSettingsDto dto)
    {
        if (dto.Https.Port is < 1 or > 65535)
            throw new ArgumentException("Der HTTPS-Port muss zwischen 1 und 65535 liegen.");

        if (dto.Jwt.TokenLifetimeHours is < 1 or > 720)
            throw new ArgumentException("Die Token-Gültigkeit muss zwischen 1 und 720 Stunden liegen.");

        if (string.IsNullOrWhiteSpace(dto.Jwt.Issuer) || string.IsNullOrWhiteSpace(dto.Jwt.Audience))
            throw new ArgumentException("Issuer und Audience dürfen nicht leer sein.");

        if (!Enum.TryParse<ClientAuthenticationMode>(dto.ClientApi.Authentication, ignoreCase: true, out var mode)
            || !Enum.IsDefined(mode))
        {
            throw new ArgumentException(
                $"Ungültiger Wert '{dto.ClientApi.Authentication}' für die Client-Authentifizierung. " +
                $"Erlaubt: {string.Join(", ", Enum.GetNames<ClientAuthenticationMode>())}.");
        }

        var path = Trim(dto.Https.CertificatePath);
        if (path != null && !File.Exists(path))
        {
            throw new ArgumentException($"Die Zertifikatsdatei '{path}' existiert nicht.");
        }

        foreach (var origin in dto.CorsAllowedOrigins.Where(o => !string.IsNullOrWhiteSpace(o)))
        {
            if (!Uri.TryCreate(origin.Trim(), UriKind.Absolute, out var uri)
                || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                throw new ArgumentException(
                    $"'{origin}' ist keine gültige Herkunft. Erwartet wird z. B. https://printermanager.firma.de");
            }
        }
    }

    /// <summary>
    /// Findet Einstellungen, bei denen eine andere Quelle (Umgebungsvariable,
    /// appsettings.Production.json, Kommandozeile) den Wert aus der Datei überstimmt.
    /// Ohne diesen Hinweis wundert man sich, warum eine Änderung folgenlos bleibt.
    /// </summary>
    private List<string> FindOverriddenKeys(LocalSettingsFile file)
    {
        var overridden = new List<string>();

        foreach (var key in TrackedKeys)
        {
            var inFile = file.Read(key);
            if (inFile == null)
                continue;

            if (!string.Equals(_configuration[key], inFile, StringComparison.Ordinal))
            {
                overridden.Add(key);
            }
        }

        return overridden;
    }
}
