using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Security.Cryptography;
using System.Text;

namespace PrinterManager.Server.Security;

/// <summary>
/// Schützt die Client-Endpunkte (Registrierung/Polling) mit einem gemeinsamen Schlüssel.
/// Die Clients haben kein JWT, deshalb senden sie den Schlüssel im Header
/// <see cref="ClientApiKeyFilter.HeaderName"/>.
/// </summary>
/// <remarks>
/// Die Prüfung ist erst aktiv, wenn <c>ClientApi:RequireKey</c> auf <c>true</c> steht.
/// Eine frische Installation läuft dadurch ohne Verteilung des Schlüssels; der Server
/// weist beim Start darauf hin (siehe <see cref="Setup.StartupReport"/>).
/// </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class ClientApiKeyAttribute : TypeFilterAttribute
{
    public ClientApiKeyAttribute() : base(typeof(ClientApiKeyFilter))
    {
    }
}

public sealed class ClientApiKeyFilter : IAuthorizationFilter
{
    public const string HeaderName = "X-Client-Key";
    public const string ConfigurationKey = "ClientApi:Key";
    public const string RequireKeyConfigurationKey = "ClientApi:RequireKey";

    private readonly string? _expectedKey;
    private readonly bool _required;
    private readonly ILogger<ClientApiKeyFilter> _logger;

    public ClientApiKeyFilter(IConfiguration configuration, ILogger<ClientApiKeyFilter> logger)
    {
        _expectedKey = configuration[ConfigurationKey];
        _required = IsRequired(configuration);
        _logger = logger;
    }

    /// <summary>
    /// Die Prüfung greift nur, wenn sie ausdrücklich verlangt wird UND ein Schlüssel
    /// hinterlegt ist — sonst würde ein Konfigurationsfehler alle Clients aussperren.
    /// </summary>
    public static bool IsRequired(IConfiguration configuration) =>
        configuration.GetValue(RequireKeyConfigurationKey, false)
        && !string.IsNullOrEmpty(configuration[ConfigurationKey]);

    public void OnAuthorization(AuthorizationFilterContext context)
    {
        if (!_required || string.IsNullOrEmpty(_expectedKey))
        {
            return;
        }

        var provided = context.HttpContext.Request.Headers[HeaderName].ToString();

        if (!FixedTimeEquals(provided, _expectedKey))
        {
            _logger.LogWarning(
                "Client-Request ohne gültigen {Header} von {RemoteIp} abgelehnt",
                HeaderName,
                context.HttpContext.Connection.RemoteIpAddress);

            context.Result = new UnauthorizedResult();
        }
    }

    private static bool FixedTimeEquals(string provided, string expected)
    {
        // FixedTimeEquals liefert false bei unterschiedlicher Länge, ohne zu werfen.
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(provided),
            Encoding.UTF8.GetBytes(expected));
    }
}
