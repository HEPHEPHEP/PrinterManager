using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PrinterManager.Server.Data;
using PrinterManager.Shared.Models;

namespace PrinterManager.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
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
        var config = await _context.SystemConfigurations.FirstAsync();
        return Ok(config);
    }

    [HttpPut]
    public async Task<ActionResult<SystemConfiguration>> Update([FromBody] SystemConfiguration dto)
    {
        var config = await _context.SystemConfigurations.FirstAsync();
        config.AssignmentPriority = dto.AssignmentPriority;
        config.AutoAssignReplacementPrinters = dto.AutoAssignReplacementPrinters;
        config.LastModified = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return Ok(config);
    }
}
