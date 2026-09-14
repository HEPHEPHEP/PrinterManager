using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PrinterManager.Server.Data;
using PrinterManager.Shared.Models;

namespace PrinterManager.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ConfigurationController : ControllerBase
{
    private readonly PrinterManagerDbContext _context;

    public ConfigurationController(PrinterManagerDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<SystemConfiguration>> Get()
    {
        return Ok(await GetOrCreateConfigAsync());
    }

    [HttpPut]
    [Authorize(Roles = "Administrator")]
    public async Task<ActionResult<SystemConfiguration>> Update([FromBody] SystemConfiguration dto)
    {
        if (!Enum.IsDefined(dto.AssignmentPriority))
            return BadRequest(new { message = "Ungültige Priorität." });

        var config = await GetOrCreateConfigAsync();
        config.AssignmentPriority = dto.AssignmentPriority;
        config.AutoAssignReplacementPrinters = dto.AutoAssignReplacementPrinters;
        config.LastModified = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return Ok(config);
    }

    private async Task<SystemConfiguration> GetOrCreateConfigAsync()
    {
        var config = await _context.SystemConfigurations.FirstOrDefaultAsync();
        if (config != null)
            return config;

        config = new SystemConfiguration { Id = 1 };
        _context.SystemConfigurations.Add(config);
        await _context.SaveChangesAsync();
        return config;
    }
}
