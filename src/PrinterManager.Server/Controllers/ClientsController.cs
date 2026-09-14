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

    public ClientsController(IClientService clientService, PrinterManagerDbContext context)
    {
        _clientService = clientService;
        _context = context;
    }

    /// <summary>Wird vom Client-Dienst aufgerufen — kein JWT, dafür der Client-API-Key.</summary>
    [HttpPost("register")]
    [AllowAnonymous]
    [ClientApiKey]
    public async Task<ActionResult<PrinterActionsResponse>> Register([FromBody] ClientRegistrationDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Hostname) || string.IsNullOrWhiteSpace(dto.UserPrincipalName))
            return BadRequest(new { message = "Hostname und UserPrincipalName sind erforderlich." });

        var response = await _clientService.RegisterClientAsync(dto);
        return Ok(response);
    }

    /// <summary>Wird vom Client-Dienst aufgerufen — kein JWT, dafür der Client-API-Key.</summary>
    [HttpGet("actions")]
    [AllowAnonymous]
    [ClientApiKey]
    public async Task<ActionResult<PrinterActionsResponse>> GetActions(
        [FromQuery] string hostname,
        [FromQuery] string userPrincipalName)
    {
        if (string.IsNullOrWhiteSpace(hostname) || string.IsNullOrWhiteSpace(userPrincipalName))
            return BadRequest(new { message = "hostname und userPrincipalName sind erforderlich." });

        var response = await _clientService.GetPrinterActionsAsync(hostname, userPrincipalName);
        return Ok(response);
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
