using Microsoft.EntityFrameworkCore;
using PrinterManager.Server.Data;
using PrinterManager.Shared.Models;
using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace PrinterManager.Server.Setup;

/// <summary>Ergebnis der HTTPS-Einrichtung, wird beim Start ausgegeben.</summary>
public sealed record HttpsSetupResult(
    bool Enabled,
    int Port,
    string Source,
    string? Thumbprint,
    bool IsSelfSigned,
    IReadOnlyList<string> Warnings)
{
    public static HttpsSetupResult Off(params string[] warnings) =>
        new(false, 0, "deaktiviert", null, false, warnings);
}

/// <summary>
/// Richtet den HTTPS-Endpunkt ein. Das Zertifikat kommt — in dieser Reihenfolge — aus
/// dem Windows-Zertifikatspeicher, einer PFX-Datei, der über die Oberfläche gepflegten
/// SSL-Konfiguration oder wird selbst signiert erzeugt.
/// </summary>
public static class HttpsSetup
{
    public const int DefaultPort = 5443;
    public const string SelfSignedFileName = "printermanager-selfsigned.pfx";

    private const string EnabledKey = "Https:Enabled";
    private const string PortKey = "Https:Port";
    private const string ThumbprintKey = "Https:CertificateThumbprint";
    private const string SubjectKey = "Https:CertificateSubject";
    private const string PathKey = "Https:CertificatePath";
    private const string PasswordKey = "Https:CertificatePassword";

    public static HttpsSetupResult Configure(WebApplicationBuilder builder)
    {
        var warnings = new List<string>();

        if (!builder.Configuration.GetValue(EnabledKey, true))
        {
            return HttpsSetupResult.Off();
        }

        var certificate = Resolve(builder.Configuration, builder.Environment.ContentRootPath, warnings,
            out var source, out var isSelfSigned);

        if (certificate == null)
        {
            warnings.Add("Es konnte kein Zertifikat ermittelt werden — HTTPS bleibt aus.");
            return HttpsSetupResult.Off(warnings.ToArray());
        }

        var port = builder.Configuration.GetValue(PortKey, DefaultPort);

        // Ein eigener Kestrel-Endpunkt setzt "Urls"/--urls/ASPNETCORE_URLS außer Kraft.
        // Deshalb wird der bestehende HTTP-Endpunkt mit übernommen, statt ihn zu verlieren.
        if (!builder.Configuration.GetSection("Kestrel:Endpoints").GetChildren().Any())
        {
            var endpoints = new Dictionary<string, string?>();
            var httpUrls = (builder.Configuration["Urls"] ?? "http://0.0.0.0:5000")
                .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            for (var i = 0; i < httpUrls.Length; i++)
            {
                endpoints[$"Kestrel:Endpoints:Http{i}:Url"] = httpUrls[i];
            }

            endpoints["Kestrel:Endpoints:Https:Url"] = $"https://0.0.0.0:{port}";
            builder.Configuration.AddInMemoryCollection(endpoints);
        }

        builder.WebHost.ConfigureKestrel(options =>
        {
            options.ConfigureHttpsDefaults(https => https.ServerCertificate = certificate);
        });

        return new HttpsSetupResult(
            true, port, source, certificate.Thumbprint, isSelfSigned, warnings);
    }

    private static X509Certificate2? Resolve(
        IConfiguration configuration,
        string contentRootPath,
        List<string> warnings,
        out string source,
        out bool isSelfSigned)
    {
        isSelfSigned = false;

        // 1. Windows-Zertifikatspeicher (z. B. per AD CS automatisch ausgestellt)
        var thumbprint = configuration[ThumbprintKey];
        var subject = configuration[SubjectKey];

        if (!string.IsNullOrWhiteSpace(thumbprint) || !string.IsNullOrWhiteSpace(subject))
        {
            var fromStore = LoadFromStore(thumbprint, subject, warnings);
            if (fromStore != null)
            {
                source = "Windows-Zertifikatspeicher";
                return fromStore;
            }
        }

        // 2. PFX aus der Konfiguration
        var configuredPath = configuration[PathKey];
        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            var fromFile = LoadFromFile(configuredPath, configuration[PasswordKey], warnings);
            if (fromFile != null)
            {
                source = $"PFX-Datei ({configuredPath})";
                return fromFile;
            }
        }

        // 3. Über die Oberfläche gepflegte SSL-Konfiguration
        var stored = ReadStoredConfiguration(configuration, warnings);
        if (stored is { Enabled: true } && !string.IsNullOrWhiteSpace(stored.CertificatePath))
        {
            var fromDb = LoadFromFile(stored.CertificatePath, stored.CertificatePassword, warnings);
            if (fromDb != null)
            {
                source = $"SSL-Konfiguration der Oberfläche ({stored.CertificatePath})";
                return fromDb;
            }
        }

        // 4. Selbst signiertes Zertifikat, damit HTTPS ohne Vorbereitung nutzbar ist
        isSelfSigned = true;
        source = "selbst signiert";
        return LoadOrCreateSelfSigned(contentRootPath, warnings);
    }

    private static X509Certificate2? LoadFromStore(string? thumbprint, string? subject, List<string> warnings)
    {
        if (!OperatingSystem.IsWindows())
        {
            warnings.Add("Der Zertifikatspeicher steht nur unter Windows zur Verfügung.");
            return null;
        }

        foreach (var location in new[] { StoreLocation.LocalMachine, StoreLocation.CurrentUser })
        {
            try
            {
                using var store = new X509Store(StoreName.My, location);
                store.Open(OpenFlags.ReadOnly);

                var matches = !string.IsNullOrWhiteSpace(thumbprint)
                    ? store.Certificates.Find(X509FindType.FindByThumbprint, Normalize(thumbprint), false)
                    : store.Certificates.Find(X509FindType.FindBySubjectName, subject!, false);

                foreach (var candidate in matches.OfType<X509Certificate2>().OrderByDescending(c => c.NotAfter))
                {
                    if (!candidate.HasPrivateKey)
                    {
                        warnings.Add($"Zertifikat {candidate.Thumbprint} hat keinen privaten Schlüssel.");
                        continue;
                    }

                    return candidate;
                }
            }
            catch (Exception ex)
            {
                warnings.Add($"Zertifikatspeicher {location} nicht lesbar: {ex.Message}");
            }
        }

        warnings.Add("Im Zertifikatspeicher wurde kein passendes Zertifikat gefunden.");
        return null;
    }

    /// <summary>Entfernt Leerzeichen und unsichtbare Zeichen, die beim Kopieren mitkommen.</summary>
    private static string Normalize(string thumbprint) =>
        new(thumbprint.Where(char.IsLetterOrDigit).ToArray());

    private static X509Certificate2? LoadFromFile(string path, string? password, List<string> warnings)
    {
        try
        {
            if (!File.Exists(path))
            {
                warnings.Add($"Zertifikatsdatei nicht gefunden: {path}");
                return null;
            }

            return new X509Certificate2(path, password);
        }
        catch (Exception ex)
        {
            warnings.Add($"Zertifikat {path} konnte nicht geladen werden: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Liest die über die Oberfläche gepflegte SSL-Konfiguration. Läuft vor dem Aufbau des
    /// Hosts, deshalb mit einem eigenen, kurzlebigen DbContext.
    /// </summary>
    private static SslConfiguration? ReadStoredConfiguration(
        IConfiguration configuration, List<string> warnings)
    {
        try
        {
            var options = new DbContextOptionsBuilder<PrinterManagerDbContext>()
                .UseSqlite(configuration.GetConnectionString("DefaultConnection"))
                .Options;

            using var db = new PrinterManagerDbContext(options);

            // Beim allerersten Start existiert die Datenbank noch nicht.
            if (!db.Database.CanConnect())
                return null;

            return db.SslConfigurations.AsNoTracking().FirstOrDefault();
        }
        catch (Exception ex)
        {
            warnings.Add($"SSL-Konfiguration konnte nicht gelesen werden: {ex.Message}");
            return null;
        }
    }

    private static X509Certificate2? LoadOrCreateSelfSigned(string contentRootPath, List<string> warnings)
    {
        var path = Path.Combine(contentRootPath, SelfSignedFileName);

        if (File.Exists(path))
        {
            var existing = LoadFromFile(path, null, warnings);

            // Ein abgelaufenes Zertifikat wird ersetzt statt weiterverwendet.
            if (existing != null && existing.NotAfter > DateTime.Now.AddDays(1))
                return existing;
        }

        try
        {
            using var certificate = CreateSelfSigned();
            var export = certificate.Export(X509ContentType.Pfx);

            File.WriteAllBytes(path, export);
            LocalSecrets.RestrictToOwner(path);

            return new X509Certificate2(export, (string?)null);
        }
        catch (Exception ex)
        {
            warnings.Add($"Selbst signiertes Zertifikat konnte nicht erzeugt werden: {ex.Message}");
            return null;
        }
    }

    private static X509Certificate2 CreateSelfSigned()
    {
        var hostname = Dns.GetHostName();

        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            $"CN={hostname}", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(
            X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, true));
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(
            new OidCollection { new("1.3.6.1.5.5.7.3.1") }, true)); // Serverauthentifizierung

        var alternativeNames = new SubjectAlternativeNameBuilder();
        alternativeNames.AddDnsName(hostname);
        alternativeNames.AddDnsName("localhost");
        alternativeNames.AddIpAddress(IPAddress.Loopback);
        alternativeNames.AddIpAddress(IPAddress.IPv6Loopback);

        foreach (var address in SafeLocalAddresses(hostname))
        {
            alternativeNames.AddIpAddress(address);
        }

        request.CertificateExtensions.Add(alternativeNames.Build());

        return request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(2));
    }

    private static IEnumerable<IPAddress> SafeLocalAddresses(string hostname)
    {
        try
        {
            return Dns.GetHostAddresses(hostname);
        }
        catch (Exception)
        {
            return [];
        }
    }
}
