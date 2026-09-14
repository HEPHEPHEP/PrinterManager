using Microsoft.Extensions.Logging;
using PrinterManager.Shared.DTOs;
using System.Net.Http.Json;
using System.Net.Sockets;

namespace PrinterManager.Client.Services;

public interface IServerCommunicationService
{
    Task<PrinterActionsResponse?> RegisterAsync(
        List<InstalledPrinterDto> installedPrinters, CancellationToken cancellationToken = default);

    Task<PrinterActionsResponse?> GetActionsAsync(CancellationToken cancellationToken = default);
}

public class ServerCommunicationService : IServerCommunicationService
{
    /// <summary>Header für den gemeinsamen Client-Schlüssel (siehe Server: <c>ClientApi:Key</c>).</summary>
    public const string ClientKeyHeader = "X-Client-Key";

    private readonly HttpClient _httpClient;
    private readonly IUserSessionService _userSessionService;
    private readonly ILogger<ServerCommunicationService> _logger;
    private readonly string _hostname;

    public ServerCommunicationService(
        HttpClient httpClient,
        IUserSessionService userSessionService,
        ILogger<ServerCommunicationService> logger)
    {
        _httpClient = httpClient;
        _userSessionService = userSessionService;
        _logger = logger;
        _hostname = Environment.MachineName;
    }

    public async Task<PrinterActionsResponse?> RegisterAsync(
        List<InstalledPrinterDto> installedPrinters, CancellationToken cancellationToken = default)
    {
        var userPrincipalName = _userSessionService.GetLoggedInUser();

        var dto = new ClientRegistrationDto
        {
            Hostname = _hostname,
            UserPrincipalName = userPrincipalName,
            IpAddress = GetLocalIpAddress(),
            OperatingSystem = Environment.OSVersion.ToString(),
            InstalledPrinters = installedPrinters
        };

        _logger.LogDebug("Registrierung: {Hostname}, Benutzer: {User}, Drucker: {Count}",
            _hostname, userPrincipalName, installedPrinters.Count);

        return await SendAsync(
            () => _httpClient.PostAsJsonAsync("/api/clients/register", dto, cancellationToken),
            cancellationToken);
    }

    public async Task<PrinterActionsResponse?> GetActionsAsync(CancellationToken cancellationToken = default)
    {
        var userPrincipalName = _userSessionService.GetLoggedInUser();

        var url = "/api/clients/actions" +
                  $"?hostname={Uri.EscapeDataString(_hostname)}" +
                  $"&userPrincipalName={Uri.EscapeDataString(userPrincipalName)}";

        _logger.LogDebug("Aktionen abrufen für {Hostname}, Benutzer: {User}", _hostname, userPrincipalName);

        return await SendAsync(() => _httpClient.GetAsync(url, cancellationToken), cancellationToken);
    }

    private async Task<PrinterActionsResponse?> SendAsync(
        Func<Task<HttpResponseMessage>> send, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await send();

            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                _logger.LogError(
                    "Der Server hat die Anfrage abgelehnt (401). Stimmt \"ClientApiKey\" mit dem " +
                    "Serverwert \"ClientApi:Key\" überein?");
                return null;
            }

            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<PrinterActionsResponse>(cancellationToken);
            _logger.LogDebug("{Count} Aktionen empfangen", result?.Actions.Count ?? 0);
            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning("Server nicht erreichbar ({BaseAddress}): {Message}",
                _httpClient.BaseAddress, ex.Message);
            return null;
        }
        catch (TaskCanceledException)
        {
            _logger.LogWarning("Zeitüberschreitung bei der Serveranfrage ({BaseAddress})", _httpClient.BaseAddress);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler bei der Kommunikation mit dem Server");
            return null;
        }
    }

    private string GetLocalIpAddress()
    {
        try
        {
            var host = System.Net.Dns.GetHostEntry(System.Net.Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    return ip.ToString();
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Lokale IP-Adresse konnte nicht ermittelt werden");
        }

        return "Unknown";
    }
}
