using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PrinterManager.Server.Data;
using PrinterManager.Server.Services;
using PrinterManager.Shared.DTOs;

namespace PrinterManager.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ClientsController : ControllerBase
{
    private readonly IClientService _clientService;
    private readonly PrinterManagerDbContext _context;

    public ClientsController(IClientService clientService, PrinterManagerDbContext context)
    {
        _clientService = clientService;
        _context = context;
    }

    [HttpPost("register")]
    public async Task<ActionResult<PrinterActionsResponse>> Register([FromBody] ClientRegistrationDto dto)
    {
        var response = await _clientService.RegisterClientAsync(dto);
        return Ok(response);
    }

    [HttpGet("actions")]
    public async Task<ActionResult<PrinterActionsResponse>> GetActions(
        [FromQuery] string hostname,
        [FromQuery] string userPrincipalName)
    {
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
    public async Task<ActionResult> DeleteUser(int id)
    {
        var user = await _context.Users.FindAsync(id);
        if (user == null)
            return NotFound();

        _context.Users.Remove(user);
        await _context.SaveChangesAsync();
        return NoContent();
    }
}
