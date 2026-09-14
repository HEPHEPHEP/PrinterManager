using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PrinterManager.Server.Data;
using PrinterManager.Server.Security;
using PrinterManager.Server.Services;
using PrinterManager.Shared.DTOs;

namespace PrinterManager.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ClientsController : ControllerBase
{
    private readonly IClientService _clientService;
    private readonly PrinterManagerDbContext _context;
    private readonly ILogger<ClientsController> _logger;

    public ClientsController(
        IClientService clientService,
        PrinterManagerDbContext context,
        ILogger<ClientsController> logger)
    {
        _clientService = clientService;
        _context = context;
        _logger = logger;
    }

    /// <summary>Wird vom Client-Dienst aufgerufen — kein JWT, siehe ClientApi:Authentication.</summary>
    [HttpPost("register")]
    [AllowAnonymous]
    [ClientAuthentication]
    public async Task<ActionResult<PrinterActionsResponse>> Register([FromBody] ClientRegistrationDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Hostname))
            return BadRequest(new { message = "Hostname ist erforderlich." });

        var user = ResolveUser(dto.UserPrincipalName);
        if (user == null)
            return BadRequest(new { message = "UserPrincipalName ist erforderlich." });

        // Ab hier gilt die ermittelte Identität, nicht die Angabe aus dem Request.
        dto.UserPrincipalName = user;

        var response = await _clientService.RegisterClientAsync(dto);
        return Ok(response);
    }

    /// <summary>Wird vom Client-Dienst aufgerufen — kein JWT, siehe ClientApi:Authentication.</summary>
    [HttpGet("actions")]
    [AllowAnonymous]
    [ClientAuthentication]
    public async Task<ActionResult<PrinterActionsResponse>> GetActions(
        [FromQuery] string hostname,
        [FromQuery] string userPrincipalName)
    {
        if (string.IsNullOrWhiteSpace(hostname))
            return BadRequest(new { message = "hostname ist erforderlich." });

        var user = ResolveUser(userPrincipalName);
        if (user == null)
            return BadRequest(new { message = "userPrincipalName ist erforderlich." });

        var response = await _clientService.GetPrinterActionsAsync(hostname, user);
        return Ok(response);
    }

    /// <summary>
    /// Liefert den Benutzer, für den gearbeitet wird. Ist die Anfrage authentifiziert
    /// (Windows-Modus), zählt ausschließlich die Identität aus dem Kerberos-Ticket — sonst
    /// könnte sich jeder Client als beliebiger Benutzer ausgeben und dessen Zuweisungen
    /// abrufen. Ohne Authentifizierung bleibt nur die Angabe des Clients.
    /// </summary>
    private string? ResolveUser(string? claimed)
    {
        var identity = User.Identity;
        var authenticated = identity is { IsAuthenticated: true } ? identity.Name : null;

        if (string.IsNullOrEmpty(authenticated))
        {
            return string.IsNullOrWhiteSpace(claimed) ? null : claimed;
        }

        if (!string.IsNullOrWhiteSpace(claimed)
            && !string.Equals(claimed, authenticated, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning(
                "Client gab {Claimed} an, authentifiziert ist aber {Authenticated} — maßgeblich " +
                "ist die authentifizierte Identität",
                claimed, authenticated);
        }

        return authenticated;
    }

    [HttpGet]
    public async Task<ActionResult> GetAll()
    {
        var clients = await _context.Clients
            .Select(c => new
            {
                c.Id,
                c.Hostname,
                c.IpAddress,
                c.OperatingSystem,
                c.LastSeen,
                c.IsActive
            })
            .ToListAsync();

        return Ok(clients);
    }

    [HttpGet("users")]
    public async Task<ActionResult> GetAllUsers()
    {
        var users = await _context.Users
            .Select(u => new
            {
                u.Id,
                u.UserPrincipalName,
                u.DisplayName,
                u.LastSeen,
                u.IsActive
            })
            .ToListAsync();

        return Ok(users);
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Administrator")]
    public async Task<ActionResult> DeleteClient(int id)
    {
        var client = await _context.Clients.FindAsync(id);
        if (client == null)
            return NotFound();

        _context.Clients.Remove(client);
        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("users/{id}")]
    [Authorize(Roles = "Administrator")]
    public async Task<ActionResult> DeleteUser(int id)
    {
        var user = await _context.Users.FindAsync(id);
        if (user == null)
            return NotFound();

        _context.Users.Remove(user);
        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("{id}/printers")]
    public async Task<ActionResult> GetClientPrinters(int id)
    {
        var clientExists = await _context.Clients.AnyAsync(c => c.Id == id);
        if (!clientExists)
            return NotFound();

        var printers = await _context.ClientPrinters
            .Where(p => p.ClientId == id)
            .Select(p => new
            {
                p.Id,
                p.PrinterName,
                p.PrinterPath,
                p.IsDefault,
                p.ManagedPrinterId,
                p.DetectedAt
            })
            .ToListAsync();

        return Ok(printers);
    }
}
