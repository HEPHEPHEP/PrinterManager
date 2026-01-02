using System.Net.Http.Json;
using PrinterManager.Shared.DTOs;
using PrinterManager.Shared.Models;

namespace PrinterManager.Web.Services;

public interface IApiService
{
    // Printers
    Task<List<PrinterDto>> GetPrintersAsync();
    Task<PrinterDto?> GetPrinterAsync(int id);
    Task<PrinterDto> CreatePrinterAsync(CreatePrinterDto dto);
    Task<PrinterDto?> UpdatePrinterAsync(int id, UpdatePrinterDto dto);
    Task<bool> DeletePrinterAsync(int id);
    Task<bool> SetPrinterAvailabilityAsync(int id, bool isAvailable);

    // Assignments
    Task<List<AssignmentDto>> GetAssignmentsAsync();
    Task<AssignmentDto> CreateAssignmentAsync(CreateAssignmentDto dto);
    Task<bool> DeleteAssignmentAsync(int id);

    // Clients and Users
    Task<List<ClientInfo>> GetClientsAsync();
    Task<List<UserInfo>> GetUsersAsync();

    // Print Server Scan
    Task<List<ScannedPrinterDto>> ScanPrintServerAsync(PrintServerScanDto dto);

    // Configuration
    Task<SystemConfiguration> GetConfigurationAsync();
    Task<SystemConfiguration> UpdateConfigurationAsync(SystemConfiguration config);
}

public class ApiService : IApiService
{
    private readonly HttpClient _httpClient;

    public ApiService(HttpClient httpClient, IConfiguration configuration)
    {
        var serverUrl = configuration["ApiUrl"] ?? "http://localhost:5000";
        _httpClient = httpClient;
        _httpClient.BaseAddress = new Uri(serverUrl);
    }

    // Printers
    public async Task<List<PrinterDto>> GetPrintersAsync()
    {
        return await _httpClient.GetFromJsonAsync<List<PrinterDto>>("/api/printers") ?? new List<PrinterDto>();
    }

    public async Task<PrinterDto?> GetPrinterAsync(int id)
    {
        return await _httpClient.GetFromJsonAsync<PrinterDto>($"/api/printers/{id}");
    }

    public async Task<PrinterDto> CreatePrinterAsync(CreatePrinterDto dto)
    {
        var response = await _httpClient.PostAsJsonAsync("/api/printers", dto);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<PrinterDto>())!;
    }

    public async Task<PrinterDto?> UpdatePrinterAsync(int id, UpdatePrinterDto dto)
    {
        var response = await _httpClient.PutAsJsonAsync($"/api/printers/{id}", dto);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<PrinterDto>();
    }

    public async Task<bool> DeletePrinterAsync(int id)
    {
        var response = await _httpClient.DeleteAsync($"/api/printers/{id}");
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> SetPrinterAvailabilityAsync(int id, bool isAvailable)
    {
        var response = await _httpClient.PostAsJsonAsync($"/api/printers/{id}/availability", isAvailable);
        return response.IsSuccessStatusCode;
    }

    // Assignments
    public async Task<List<AssignmentDto>> GetAssignmentsAsync()
    {
        return await _httpClient.GetFromJsonAsync<List<AssignmentDto>>("/api/assignments") ?? new List<AssignmentDto>();
    }

    public async Task<AssignmentDto> CreateAssignmentAsync(CreateAssignmentDto dto)
    {
        var response = await _httpClient.PostAsJsonAsync("/api/assignments", dto);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AssignmentDto>())!;
    }

    public async Task<bool> DeleteAssignmentAsync(int id)
    {
        var response = await _httpClient.DeleteAsync($"/api/assignments/{id}");
        return response.IsSuccessStatusCode;
    }

    // Clients and Users
    public async Task<List<ClientInfo>> GetClientsAsync()
    {
        return await _httpClient.GetFromJsonAsync<List<ClientInfo>>("/api/clients") ?? new List<ClientInfo>();
    }

    public async Task<List<UserInfo>> GetUsersAsync()
    {
        return await _httpClient.GetFromJsonAsync<List<UserInfo>>("/api/clients/users") ?? new List<UserInfo>();
    }

    // Print Server Scan
    public async Task<List<ScannedPrinterDto>> ScanPrintServerAsync(PrintServerScanDto dto)
    {
        var response = await _httpClient.PostAsJsonAsync("/api/printserver/scan", dto);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<ScannedPrinterDto>>() ?? new List<ScannedPrinterDto>();
    }

    // Configuration
    public async Task<SystemConfiguration> GetConfigurationAsync()
    {
        return (await _httpClient.GetFromJsonAsync<SystemConfiguration>("/api/configuration"))!;
    }

    public async Task<SystemConfiguration> UpdateConfigurationAsync(SystemConfiguration config)
    {
        var response = await _httpClient.PutAsJsonAsync("/api/configuration", config);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<SystemConfiguration>())!;
    }
}

public class ClientInfo
{
    public int Id { get; set; }
    public string Hostname { get; set; } = string.Empty;
    public string? IpAddress { get; set; }
    public string? OperatingSystem { get; set; }
    public DateTime LastSeen { get; set; }
    public bool IsActive { get; set; }
}

public class UserInfo
{
    public int Id { get; set; }
    public string UserPrincipalName { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public DateTime LastSeen { get; set; }
    public bool IsActive { get; set; }
}
