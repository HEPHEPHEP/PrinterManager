using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Negotiate;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Security.Cryptography;
using System.Text;

namespace PrinterManager.Server.Security;

/// <summary>Wie sich der Client-Dienst gegenüber dem Server ausweist.</summary>
public enum ClientAuthenticationMode
{
    /// <summary>Keine Prüfung — nur für die Inbetriebnahme gedacht.</summary>
    None,

    /// <summary>Gemeinsamer Schlüssel im Header <c>X-Client-Key</c>.</summary>
    ApiKey,

    /// <summary>
    /// Kerberos/NTLM über das Negotiate-Verfahren. Der Server kennt damit den aufrufenden
    /// Benutzer und muss sich nicht auf dessen eigene Angabe verlassen.
    /// </summary>
    Windows
}

public static class ClientAuthenticationOptions
{
    public const string ModeKey = "ClientApi:Authentication";
    public const string ApiKeyKey = "ClientApi:Key";
    public const string HeaderName = "X-Client-Key";

    public static ClientAuthenticationMode GetMode(IConfiguration configuration)
    {
        var configured = configuration[ModeKey];

        if (string.IsNullOrWhiteSpace(configured))
            return ClientAuthenticationMode.None;

        if (!Enum.TryParse<ClientAuthenticationMode>(configured, ignoreCase: true, out var mode)
            || !Enum.IsDefined(mode))
        {
            throw new InvalidOperationException(
                $"Ungültiger Wert '{configured}' für {ModeKey}. Erlaubt: " +
                $"{string.Join(", ", Enum.GetNames<ClientAuthenticationMode>())}.");
        }

        // Ein Schlüsselmodus ohne hinterlegten Schlüssel würde alle Clients aussperren.
        if (mode == ClientAuthenticationMode.ApiKey && string.IsNullOrEmpty(configuration[ApiKeyKey]))
        {
            return ClientAuthenticationMode.None;
        }

        return mode;
    }
}

/// <summary>
/// Schützt die Client-Endpunkte. Die Clients haben kein JWT, deshalb greift hier
/// je nach <c>ClientApi:Authentication</c> ein gemeinsamer Schlüssel oder Kerberos.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class ClientAuthenticationAttribute : TypeFilterAttribute
{
    public ClientAuthenticationAttribute() : base(typeof(ClientAuthenticationFilter))
    {
    }
}

public sealed class ClientAuthenticationFilter : IAsyncAuthorizationFilter
{
    private readonly ClientAuthenticationMode _mode;
    private readonly string? _expectedKey;
    private readonly ILogger<ClientAuthenticationFilter> _logger;

    public ClientAuthenticationFilter(IConfiguration configuration, ILogger<ClientAuthenticationFilter> logger)
    {
        _mode = ClientAuthenticationOptions.GetMode(configuration);
        _expectedKey = configuration[ClientAuthenticationOptions.ApiKeyKey];
        _logger = logger;
    }

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        switch (_mode)
        {
            case ClientAuthenticationMode.ApiKey:
                CheckApiKey(context);
                break;

            case ClientAuthenticationMode.Windows:
                await CheckWindowsAsync(context);
                break;

            case ClientAuthenticationMode.None:
            default:
                break;
        }
    }

    private void CheckApiKey(AuthorizationFilterContext context)
    {
        var provided = context.HttpContext.Request.Headers[ClientAuthenticationOptions.HeaderName].ToString();

        if (FixedTimeEquals(provided, _expectedKey ?? string.Empty))
            return;

        _logger.LogWarning("Client-Request ohne gültigen {Header} von {RemoteIp} abgelehnt",
            ClientAuthenticationOptions.HeaderName, context.HttpContext.Connection.RemoteIpAddress);

        context.Result = new UnauthorizedResult();
    }

    /// <summary>
    /// Negotiate läuft nicht als Standardschema (das ist JWT für die Weboberfläche),
    /// deshalb wird es hier ausdrücklich angestoßen und das Ergebnis übernommen.
    /// </summary>
    private async Task CheckWindowsAsync(AuthorizationFilterContext context)
    {
        var result = await context.HttpContext.AuthenticateAsync(NegotiateDefaults.AuthenticationScheme);

        if (!result.Succeeded || result.Principal is not { Identity.IsAuthenticated: true } principal)
        {
            // ChallengeResult sendet "WWW-Authenticate: Negotiate" — ein blankes 401 nicht.
            context.Result = new ChallengeResult(NegotiateDefaults.AuthenticationScheme);
            return;
        }

        context.HttpContext.User = principal;
    }

    private static bool FixedTimeEquals(string provided, string expected) =>
        CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(provided),
            Encoding.UTF8.GetBytes(expected));
}
