using Microsoft.EntityFrameworkCore;
using PrinterManager.Server.Data;
using System.DirectoryServices.Protocols;
using System.Net;
using System.Text;

namespace PrinterManager.Server.Services;

public interface ILdapService
{
    Task<LdapAuthResult> AuthenticateAsync(string username, string password);
    Task<bool> IsEnabledAsync();
}

public class LdapAuthResult
{
    public bool Success { get; set; }
    public string? FullName { get; set; }
    public string? Email { get; set; }
    public string? Message { get; set; }
}

public class LdapService : ILdapService
{
    private const int LdapsPort = 636;

    private readonly PrinterManagerDbContext _context;
    private readonly ILogger<LdapService> _logger;

    public LdapService(PrinterManagerDbContext context, ILogger<LdapService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<bool> IsEnabledAsync()
    {
        var config = await _context.LdapConfigurations.FirstOrDefaultAsync();
        return config?.Enabled ?? false;
    }

    public async Task<LdapAuthResult> AuthenticateAsync(string username, string password)
    {
        // Ein Bind mit leerem Passwort gilt bei vielen LDAP-Servern als "unauthenticated
        // bind" und ist ERFOLGREICH — ohne diese Prüfung wäre das ein Auth-Bypass.
        if (string.IsNullOrEmpty(password))
        {
            return new LdapAuthResult { Success = false, Message = "Passwort erforderlich" };
        }

        if (string.IsNullOrWhiteSpace(username))
        {
            return new LdapAuthResult { Success = false, Message = "Benutzername erforderlich" };
        }

        var config = await _context.LdapConfigurations.FirstOrDefaultAsync();

        if (config == null || !config.Enabled)
        {
            return new LdapAuthResult { Success = false, Message = "LDAP is not enabled" };
        }

        if (string.IsNullOrEmpty(config.Server) || string.IsNullOrEmpty(config.BaseDn)
            || string.IsNullOrEmpty(config.UserDnTemplate))
        {
            return new LdapAuthResult { Success = false, Message = "LDAP configuration is incomplete" };
        }

        var server = config.Server;
        var port = config.Port;
        var baseDn = config.BaseDn;
        var userDn = config.UserDnTemplate.Replace("{0}", EscapeDistinguishedName(username));
        var filter = $"(uid={EscapeFilter(username)})";

        // Bind/SendRequest sind blockierend -> auf den Threadpool auslagern.
        return await Task.Run(() => Bind(server, port, baseDn, userDn, password, filter));
    }

    private LdapAuthResult Bind(string server, int port, string baseDn, string userDn, string password, string filter)
    {
        try
        {
            using var connection = new LdapConnection(new LdapDirectoryIdentifier(server, port))
            {
                AuthType = AuthType.Basic
            };
            connection.SessionOptions.ProtocolVersion = 3;

            if (port == LdapsPort)
            {
                connection.SessionOptions.SecureSocketLayer = true;
            }
            else
            {
                // Auf Port 389 werden die Zugangsdaten sonst im Klartext übertragen.
                // StartTLS schlägt fehl, wenn der Server es nicht unterstützt — dann
                // wird ohne TLS fortgefahren, aber deutlich gewarnt.
                try
                {
                    connection.SessionOptions.StartTransportLayerSecurity(null);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                        "StartTLS zu {Server}:{Port} fehlgeschlagen — Zugangsdaten werden unverschlüsselt " +
                        "übertragen. Bitte LDAPS (Port {LdapsPort}) konfigurieren.", server, port, LdapsPort);
                }
            }

            connection.Credential = new NetworkCredential(userDn, password);
            connection.Bind();

            string? fullName = null;
            string? email = null;

            try
            {
                var searchRequest = new SearchRequest(
                    baseDn, filter, SearchScope.Subtree, "cn", "mail", "displayName");

                if (connection.SendRequest(searchRequest) is SearchResponse searchResponse
                    && searchResponse.Entries.Count > 0)
                {
                    var entry = searchResponse.Entries[0];
                    fullName = GetAttribute(entry, "cn") ?? GetAttribute(entry, "displayName");
                    email = GetAttribute(entry, "mail");
                }
            }
            catch (Exception ex)
            {
                // Der Bind war erfolgreich — fehlende Stammdaten sind kein Login-Fehler.
                _logger.LogWarning(ex, "LDAP-Attribute für {UserDn} konnten nicht gelesen werden", userDn);
            }

            return new LdapAuthResult { Success = true, FullName = fullName, Email = email };
        }
        catch (LdapException ex)
        {
            _logger.LogInformation("LDAP-Bind für {UserDn} fehlgeschlagen: {Error}", userDn, ex.Message);
            return new LdapAuthResult { Success = false, Message = "LDAP-Authentifizierung fehlgeschlagen" };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler bei der LDAP-Authentifizierung für {UserDn}", userDn);
            return new LdapAuthResult { Success = false, Message = "LDAP-Authentifizierung fehlgeschlagen" };
        }
    }

    private static string? GetAttribute(SearchResultEntry entry, string name)
    {
        var attribute = entry.Attributes[name];
        return attribute is { Count: > 0 } ? attribute[0]?.ToString() : null;
    }

    /// <summary>Maskiert Sonderzeichen in LDAP-Suchfiltern (RFC 4515).</summary>
    internal static string EscapeFilter(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var c in value)
        {
            switch (c)
            {
                case '\\': builder.Append("\\5c"); break;
                case '*': builder.Append("\\2a"); break;
                case '(': builder.Append("\\28"); break;
                case ')': builder.Append("\\29"); break;
                case '\0': builder.Append("\\00"); break;
                case '/': builder.Append("\\2f"); break;
                default: builder.Append(c); break;
            }
        }
        return builder.ToString();
    }

    /// <summary>Maskiert Sonderzeichen in Distinguished Names (RFC 4514).</summary>
    internal static string EscapeDistinguishedName(string value)
    {
        var builder = new StringBuilder(value.Length);
        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];
            var isEdge = i == 0 || i == value.Length - 1;

            switch (c)
            {
                case '\\':
                case ',':
                case '+':
                case '"':
                case '<':
                case '>':
                case ';':
                case '=':
                    builder.Append('\\').Append(c);
                    break;
                case '#' when i == 0:
                case ' ' when isEdge:
                    builder.Append('\\').Append(c);
                    break;
                case '\0':
                    builder.Append("\\00");
                    break;
                default:
                    builder.Append(c);
                    break;
            }
        }
        return builder.ToString();
    }
}
