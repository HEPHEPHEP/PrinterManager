using Microsoft.AspNetCore.Mvc;
using PrinterManager.Server.Services;
using PrinterManager.Shared.DTOs;

namespace PrinterManager.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
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
        var printers = await _scanService.ScanPrintServerAsync(dto);
        return Ok(printers);
    }
}
