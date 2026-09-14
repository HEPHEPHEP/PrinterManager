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
/// Ist <c>ClientApi:Key</c> nicht konfiguriert, bleiben die Endpunkte offen, damit
/// bestehende Installationen nach einem Update nicht abreißen. Der Server warnt in
/// diesem Fall beim Start (siehe Program.cs).
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

    private readonly string? _expectedKey;
    private readonly ILogger<ClientApiKeyFilter> _logger;

    public ClientApiKeyFilter(IConfiguration configuration, ILogger<ClientApiKeyFilter> logger)
    {
        _expectedKey = configuration[ConfigurationKey];
        _logger = logger;
    }

    public void OnAuthorization(AuthorizationFilterContext context)
    {
        if (string.IsNullOrEmpty(_expectedKey))
        {
            // Kein Schlüssel konfiguriert -> Legacy-Verhalten (offen).
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
