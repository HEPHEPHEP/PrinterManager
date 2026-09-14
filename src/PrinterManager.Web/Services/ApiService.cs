using System.Net.Http.Headers;
using System.Net.Http.Json;
using PrinterManager.Shared.DTOs;
using PrinterManager.Shared.Models;

namespace PrinterManager.Web.Services;

public interface IApiService
{
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
    private readonly AuthStateService _authState;

    public ApiService(HttpClient httpClient, AuthStateService authState)
    {
        _httpClient = httpClient;
        _authState = authState;
    }

    /// <summary>
    /// Setzt den Bearer-Token vor jedem Aufruf aus dem Zustand dieses Circuits.
    /// </summary>
    /// <remarks>
    /// Bewusst kein <see cref="DelegatingHandler"/>: Handler von <c>IHttpClientFactory</c>
    /// leben in einem eigenen, über Minuten wiederverwendeten DI-Scope und würden das Token
    /// eines Benutzers an andere Verbindungen weiterreichen. Der typisierte HttpClient ist
    /// dagegen pro ApiService-Instanz eigenständig.
    /// </remarks>
    private HttpClient Client
    {
        get
        {
            _httpClient.DefaultRequestHeaders.Authorization = string.IsNullOrEmpty(_authState.Token)
                ? null
                : new AuthenticationHeaderValue("Bearer", _authState.Token);

            return _httpClient;
        }
    }

    // Auth
    public async Task<LoginResponseDto> LoginAsync(LoginDto dto)
    {
        var response = await Client.PostAsJsonAsync("/api/auth/login", dto);

        if (!response.IsSuccessStatusCode)
        {
            // Der Server liefert bei 401 selbst ein LoginResponseDto mit Meldung.
            var body = await TryReadLoginResponseAsync(response);
            return body ?? new LoginResponseDto
            {
                Success = false,
                Message = "Anmeldung fehlgeschlagen. Bitte Serververbindung prüfen."
            };
        }

        return (await response.Content.ReadFromJsonAsync<LoginResponseDto>())!;
    }

    private static async Task<LoginResponseDto?> TryReadLoginResponseAsync(HttpResponseMessage response)
    {
        try
        {
            return await response.Content.ReadFromJsonAsync<LoginResponseDto>();
        }
        catch (Exception)
        {
            // Kein JSON-Body (z. B. Reverse-Proxy-Fehlerseite) — Rohinhalt bewusst nicht anzeigen.
            return null;
        }
    }

    public async Task<List<UserDto>> GetAppUsersAsync()
    {
        return await Client.GetFromJsonAsync<List<UserDto>>("/api/auth/users") ?? new List<UserDto>();
    }

    public async Task<UserDto> RegisterAppUserAsync(RegisterUserDto dto)
    {
        var response = await Client.PostAsJsonAsync("/api/auth/register", dto);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<UserDto>())!;
    }

    public async Task<bool> DeleteAppUserAsync(int id)
    {
        var response = await Client.DeleteAsync($"/api/auth/users/{id}");
        return response.IsSuccessStatusCode;
    }

    public async Task<UserDto> UpdateUserRoleAsync(int id, string role)
    {
        var response = await Client.PutAsJsonAsync($"/api/auth/users/{id}/role", new UpdateUserRoleDto { Role = role });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<UserDto>())!;
    }

    // Security Config
    public async Task<LdapConfigDto> GetLdapConfigAsync()
    {
        return (await Client.GetFromJsonAsync<LdapConfigDto>("/api/securityconfig/ldap"))!;
    }

    public async Task<LdapConfigDto> UpdateLdapConfigAsync(LdapConfigDto dto)
    {
        var response = await Client.PutAsJsonAsync("/api/securityconfig/ldap", dto);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<LdapConfigDto>())!;
    }

    public async Task<SslConfigDto> GetSslConfigAsync()
    {
        return (await Client.GetFromJsonAsync<SslConfigDto>("/api/securityconfig/ssl"))!;
    }

    public async Task<SslConfigDto> UpdateSslConfigAsync(SslConfigDto dto)
    {
        var response = await Client.PutAsJsonAsync("/api/securityconfig/ssl", dto);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<SslConfigDto>())!;
    }

    // Printers
    public async Task<List<PrinterDto>> GetPrintersAsync()
    {
        return await Client.GetFromJsonAsync<List<PrinterDto>>("/api/printers") ?? new List<PrinterDto>();
    }

    public async Task<PrinterDto?> GetPrinterAsync(int id)
    {
        return await Client.GetFromJsonAsync<PrinterDto>($"/api/printers/{id}");
    }

    public async Task<PrinterDto> CreatePrinterAsync(CreatePrinterDto dto)
    {
        var response = await Client.PostAsJsonAsync("/api/printers", dto);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<PrinterDto>())!;
    }

    public async Task<PrinterDto?> UpdatePrinterAsync(int id, UpdatePrinterDto dto)
    {
        var response = await Client.PutAsJsonAsync($"/api/printers/{id}", dto);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<PrinterDto>();
    }

    public async Task<bool> DeletePrinterAsync(int id)
    {
        var response = await Client.DeleteAsync($"/api/printers/{id}");
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> SetPrinterAvailabilityAsync(int id, bool isAvailable)
    {
        var response = await Client.PostAsJsonAsync($"/api/printers/{id}/availability", isAvailable);
        return response.IsSuccessStatusCode;
    }

    // Assignments
    public async Task<List<AssignmentDto>> GetAssignmentsAsync()
    {
        return await Client.GetFromJsonAsync<List<AssignmentDto>>("/api/assignments") ?? new List<AssignmentDto>();
    }

    public async Task<AssignmentDto> CreateAssignmentAsync(CreateAssignmentDto dto)
    {
        var response = await Client.PostAsJsonAsync("/api/assignments", dto);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AssignmentDto>())!;
    }

    public async Task<List<AssignmentDto>> CreateBulkAssignmentsAsync(BulkAssignmentDto dto)
    {
        var response = await Client.PostAsJsonAsync("/api/assignments/bulk", dto);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<List<AssignmentDto>>())!;
    }

    public async Task<bool> DeleteAssignmentAsync(int id)
    {
        var response = await Client.DeleteAsync($"/api/assignments/{id}");
        return response.IsSuccessStatusCode;
    }

    // Clients and Users
    public async Task<List<ClientInfo>> GetClientsAsync()
    {
        return await Client.GetFromJsonAsync<List<ClientInfo>>("/api/clients") ?? new List<ClientInfo>();
    }

    public async Task<List<UserInfo>> GetUsersAsync()
    {
        return await Client.GetFromJsonAsync<List<UserInfo>>("/api/clients/users") ?? new List<UserInfo>();
    }

    public async Task<bool> DeleteClientAsync(int id)
    {
        var response = await Client.DeleteAsync($"/api/clients/{id}");
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> DeleteUserAsync(int id)
    {
        var response = await Client.DeleteAsync($"/api/clients/users/{id}");
        return response.IsSuccessStatusCode;
    }

    public async Task<List<ClientPrinterDto>> GetClientPrintersAsync(int clientId)
    {
        return await Client.GetFromJsonAsync<List<ClientPrinterDto>>($"/api/clients/{clientId}/printers") ?? new List<ClientPrinterDto>();
    }

    public async Task<List<AssignmentDto>> GetClientAssignmentsAsync(int clientId)
    {
        return await Client.GetFromJsonAsync<List<AssignmentDto>>($"/api/assignments/client/{clientId}") ?? new List<AssignmentDto>();
    }

    public async Task<List<AssignmentDto>> GetUserAssignmentsAsync(int userId)
    {
        return await Client.GetFromJsonAsync<List<AssignmentDto>>($"/api/assignments/user/{userId}") ?? new List<AssignmentDto>();
    }

    // Print Server Scan
    public async Task<List<ScannedPrinterDto>> ScanPrintServerAsync(PrintServerScanDto dto)
    {
        var response = await Client.PostAsJsonAsync("/api/printserver/scan", dto);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<ScannedPrinterDto>>() ?? new List<ScannedPrinterDto>();
    }

    // Configuration
    public async Task<SystemConfiguration> GetConfigurationAsync()
    {
        return (await Client.GetFromJsonAsync<SystemConfiguration>("/api/configuration"))!;
    }

    public async Task<SystemConfiguration> UpdateConfigurationAsync(SystemConfiguration config)
    {
        var response = await Client.PutAsJsonAsync("/api/configuration", config);
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
