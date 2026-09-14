using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PrinterManager.Server.Services;
using PrinterManager.Shared.DTOs;

namespace PrinterManager.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Administrator")]
public class PrintServerController : ControllerBase
{
    private readonly IPrintServerScanService _scanService;

    public PrintServerController(IPrintServerScanService scanService)
    {
        _scanService = scanService;
    }

    [HttpPost("scan")]
    public async Task<ActionResult<List<ScannedPrinterDto>>> Scan([FromBody] PrintServerScanDto dto)
    {
        try
        {
            var printers = await _scanService.ScanPrintServerAsync(dto);
            return Ok(printers);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            // Verbindungs-/WMI-Fehler sollen im UI sichtbar werden statt als leere Liste.
            return StatusCode(StatusCodes.Status502BadGateway, new { message = ex.Message });
        }
    }
}
