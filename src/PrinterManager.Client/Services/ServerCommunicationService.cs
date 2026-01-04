using System.Net.Http.Json;
using System.Security.Principal;
using PrinterManager.Shared.DTOs;
using Microsoft.Extensions.Logging;

namespace PrinterManager.Client.Services;

public interface IServerCommunicationService
{
    Task<PrinterActionsResponse?> RegisterAsync(List<InstalledPrinterDto> installedPrinters);
    Task<PrinterActionsResponse?> GetActionsAsync();
}

public class ServerCommunicationService : IServerCommunicationService
{
    private readonly HttpClient _httpClient;
    private readonly IUserSessionService _userSessionService;
    private readonly ILogger<ServerCommunicationService> _logger;
    private readonly string _hostname;
    private string _userPrincipalName;

    public ServerCommunicationService(
        IConfiguration configuration,
        IUserSessionService userSessionService,
        ILogger<ServerCommunicationService> logger)
    {
        var serverUrl = configuration["ServerUrl"] ?? "http://localhost:5000";
        _httpClient = new HttpClient { BaseAddress = new Uri(serverUrl) };
        _userSessionService = userSessionService;
        _logger = logger;

        _hostname = Environment.MachineName;
        _userPrincipalName = string.Empty; // Will be set dynamically
    }

    public async Task<PrinterActionsResponse?> RegisterAsync(List<InstalledPrinterDto> installedPrinters)
    {
        try
        {
            // Get current logged-in user
            _userPrincipalName = _userSessionService.GetLoggedInUser();

            var dto = new ClientRegistrationDto
            {
                Hostname = _hostname,
                UserPrincipalName = _userPrincipalName,
                IpAddress = GetLocalIpAddress(),
                OperatingSystem = Environment.OSVersion.ToString(),
                InstalledPrinters = installedPrinters
            };

            _logger.LogDebug($"Sending registration to server: {_hostname}, User: {_userPrincipalName}, Printers: {installedPrinters.Count}");
            var response = await _httpClient.PostAsJsonAsync("/api/clients/register", dto);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<PrinterActionsResponse>();
            _logger.LogDebug($"Received {result?.Actions?.Count ?? 0} actions from server");
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error registering with server");
            return null;
        }
    }

    public async Task<PrinterActionsResponse?> GetActionsAsync()
    {
        try
        {
            // Get current logged-in user
            _userPrincipalName = _userSessionService.GetLoggedInUser();

            _logger.LogDebug($"Getting actions from server for {_hostname}, User: {_userPrincipalName}");
            var response = await _httpClient.GetAsync(
                $"/api/clients/actions?hostname={_hostname}&userPrincipalName={Uri.EscapeDataString(_userPrincipalName)}");

            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<PrinterActionsResponse>();
            _logger.LogDebug($"Received {result?.Actions?.Count ?? 0} actions from server");
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting actions from server");
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
                if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                {
                    return ip.ToString();
                }
            }
        }
        catch { }

        return "Unknown";
    }
}
