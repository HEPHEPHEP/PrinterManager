using System.Net.Http.Json;
using System.Security.Principal;
using PrinterManager.Shared.DTOs;

namespace PrinterManager.Client.Services;

public interface IServerCommunicationService
{
    Task<PrinterActionsResponse?> RegisterAsync(List<InstalledPrinterDto> installedPrinters);
    Task<PrinterActionsResponse?> GetActionsAsync();
}

public class ServerCommunicationService : IServerCommunicationService
{
    private readonly HttpClient _httpClient;
    private readonly string _hostname;
    private readonly string _userPrincipalName;

    public ServerCommunicationService(IConfiguration configuration)
    {
        var serverUrl = configuration["ServerUrl"] ?? "http://localhost:5000";
        _httpClient = new HttpClient { BaseAddress = new Uri(serverUrl) };

        _hostname = Environment.MachineName;
        _userPrincipalName = WindowsIdentity.GetCurrent().Name;
    }

    public async Task<PrinterActionsResponse?> RegisterAsync(List<InstalledPrinterDto> installedPrinters)
    {
        try
        {
            var dto = new ClientRegistrationDto
            {
                Hostname = _hostname,
                UserPrincipalName = _userPrincipalName,
                IpAddress = GetLocalIpAddress(),
                OperatingSystem = Environment.OSVersion.ToString(),
                InstalledPrinters = installedPrinters
            };

            var response = await _httpClient.PostAsJsonAsync("/api/clients/register", dto);
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadFromJsonAsync<PrinterActionsResponse>();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error registering with server: {ex.Message}");
            return null;
        }
    }

    public async Task<PrinterActionsResponse?> GetActionsAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync(
                $"/api/clients/actions?hostname={_hostname}&userPrincipalName={Uri.EscapeDataString(_userPrincipalName)}");

            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<PrinterActionsResponse>();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting actions from server: {ex.Message}");
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
