using System.Net.Http.Json;
using PrinterManager.Shared.DTOs;
using PrinterManager.Shared.Models;

namespace PrinterManager.Web.Services;

public interface IApiService
{
    void SetAuthToken(string token);

    // Auth
    Task<LoginResponseDto> LoginAsync(LoginDto dto);
    Task<List<UserDto>> GetAppUsersAsync();
    Task<UserDto> RegisterAppUserAsync(RegisterUserDto dto);
    Task<bool> DeleteAppUserAsync(int id);
    Task<UserDto> UpdateUserRoleAsync(int id, string role);

    // Security Config
    Task<LdapConfigDto> GetLdapConfigAsync();
    Task<LdapConfigDto> UpdateLdapConfigAsync(LdapConfigDto dto);
    Task<SslConfigDto> GetSslConfigAsync();
    Task<SslConfigDto> UpdateSslConfigAsync(SslConfigDto dto);

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
    Task<List<AssignmentDto>> CreateBulkAssignmentsAsync(BulkAssignmentDto dto);
    Task<bool> DeleteAssignmentAsync(int id);
    Task<AssignmentDto?> SetAssignmentAsDefaultAsync(int id);

    // Clients and Users
    Task<List<ClientInfo>> GetClientsAsync();
    Task<List<UserInfo>> GetUsersAsync();
    Task<bool> DeleteClientAsync(int id);
    Task<bool> DeleteUserAsync(int id);
    Task<List<ClientPrinterDto>> GetClientPrintersAsync(int clientId);
    Task<List<AssignmentDto>> GetClientAssignmentsAsync(int clientId);
    Task<List<AssignmentDto>> GetUserAssignmentsAsync(int userId);

    // Print Server Scan
    Task<List<ScannedPrinterDto>> ScanPrintServerAsync(PrintServerScanDto dto);

    // Configuration
    Task<SystemConfiguration> GetConfigurationAsync();
    Task<SystemConfiguration> UpdateConfigurationAsync(SystemConfiguration config);
}

public class ApiService : IApiService
{
    private readonly HttpClient _httpClient;

    public ApiService(HttpClient httpClient)
    {
        _httpClient = httpClient;
        Console.WriteLine($"ApiService created with BaseAddress: {_httpClient.BaseAddress}");
    }

    public void SetAuthToken(string token)
    {
        _httpClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
    }

    // Auth
    public async Task<LoginResponseDto> LoginAsync(LoginDto dto)
    {
        var response = await _httpClient.PostAsJsonAsync("/api/auth/login", dto);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"Login failed with status {response.StatusCode}: {errorContent}");
            return new LoginResponseDto
            {
                Success = false,
                Message = $"Server returned {response.StatusCode}: {errorContent}"
            };
        }

        return (await response.Content.ReadFromJsonAsync<LoginResponseDto>())!;
    }

    public async Task<List<UserDto>> GetAppUsersAsync()
    {
        return await _httpClient.GetFromJsonAsync<List<UserDto>>("/api/auth/users") ?? new List<UserDto>();
    }

    public async Task<UserDto> RegisterAppUserAsync(RegisterUserDto dto)
    {
        var response = await _httpClient.PostAsJsonAsync("/api/auth/register", dto);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<UserDto>())!;
    }

    public async Task<bool> DeleteAppUserAsync(int id)
    {
        var response = await _httpClient.DeleteAsync($"/api/auth/users/{id}");
        return response.IsSuccessStatusCode;
    }

    public async Task<UserDto> UpdateUserRoleAsync(int id, string role)
    {
        var response = await _httpClient.PutAsJsonAsync($"/api/auth/users/{id}/role", new UpdateUserRoleDto { Role = role });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<UserDto>())!;
    }

    // Security Config
    public async Task<LdapConfigDto> GetLdapConfigAsync()
    {
        return (await _httpClient.GetFromJsonAsync<LdapConfigDto>("/api/securityconfig/ldap"))!;
    }

    public async Task<LdapConfigDto> UpdateLdapConfigAsync(LdapConfigDto dto)
    {
        var response = await _httpClient.PutAsJsonAsync("/api/securityconfig/ldap", dto);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<LdapConfigDto>())!;
    }

    public async Task<SslConfigDto> GetSslConfigAsync()
    {
        return (await _httpClient.GetFromJsonAsync<SslConfigDto>("/api/securityconfig/ssl"))!;
    }

    public async Task<SslConfigDto> UpdateSslConfigAsync(SslConfigDto dto)
    {
        var response = await _httpClient.PutAsJsonAsync("/api/securityconfig/ssl", dto);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<SslConfigDto>())!;
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

    public async Task<List<AssignmentDto>> CreateBulkAssignmentsAsync(BulkAssignmentDto dto)
    {
        var response = await _httpClient.PostAsJsonAsync("/api/assignments/bulk", dto);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<List<AssignmentDto>>())!;
    }

    public async Task<bool> DeleteAssignmentAsync(int id)
    {
        var response = await _httpClient.DeleteAsync($"/api/assignments/{id}");
        return response.IsSuccessStatusCode;
    }

    public async Task<AssignmentDto?> SetAssignmentAsDefaultAsync(int id)
    {
        var response = await _httpClient.PutAsync($"/api/assignments/{id}/set-default", null);
        if (!response.IsSuccessStatusCode)
            return null;

        return await response.Content.ReadFromJsonAsync<AssignmentDto>();
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

    public async Task<bool> DeleteClientAsync(int id)
    {
        var response = await _httpClient.DeleteAsync($"/api/clients/{id}");
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> DeleteUserAsync(int id)
    {
        var response = await _httpClient.DeleteAsync($"/api/clients/users/{id}");
        return response.IsSuccessStatusCode;
    }

    public async Task<List<ClientPrinterDto>> GetClientPrintersAsync(int clientId)
    {
        return await _httpClient.GetFromJsonAsync<List<ClientPrinterDto>>($"/api/clients/{clientId}/printers") ?? new List<ClientPrinterDto>();
    }

    public async Task<List<AssignmentDto>> GetClientAssignmentsAsync(int clientId)
    {
        return await _httpClient.GetFromJsonAsync<List<AssignmentDto>>($"/api/assignments/client/{clientId}") ?? new List<AssignmentDto>();
    }

    public async Task<List<AssignmentDto>> GetUserAssignmentsAsync(int userId)
    {
        return await _httpClient.GetFromJsonAsync<List<AssignmentDto>>($"/api/assignments/user/{userId}") ?? new List<AssignmentDto>();
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

public class ClientPrinterDto
{
    public int Id { get; set; }
    public string PrinterName { get; set; } = string.Empty;
    public string? PrinterPath { get; set; }
    public bool IsDefault { get; set; }
    public int? ManagedPrinterId { get; set; }
    public DateTime DetectedAt { get; set; }
}
