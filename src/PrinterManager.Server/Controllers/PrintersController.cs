using Microsoft.AspNetCore.Mvc;
using PrinterManager.Server.Services;
using PrinterManager.Shared.DTOs;

namespace PrinterManager.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PrintersController : ControllerBase
{
    private readonly IPrinterService _printerService;

    public PrintersController(IPrinterService printerService)
    {
        _printerService = printerService;
    }

    [HttpGet]
    public async Task<ActionResult<List<PrinterDto>>> GetAll()
    {
        var printers = await _printerService.GetAllPrintersAsync();
        return Ok(printers);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<PrinterDto>> GetById(int id)
    {
        var printer = await _printerService.GetPrinterByIdAsync(id);
        if (printer == null)
            return NotFound();

        return Ok(printer);
    }

    [HttpPost]
    public async Task<ActionResult<PrinterDto>> Create([FromBody] CreatePrinterDto dto)
    {
        var printer = await _printerService.CreatePrinterAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = printer.Id }, printer);
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<PrinterDto>> Update(int id, [FromBody] UpdatePrinterDto dto)
    {
        var printer = await _printerService.UpdatePrinterAsync(id, dto);
        if (printer == null)
            return NotFound();

        return Ok(printer);
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> Delete(int id)
    {
        var result = await _printerService.DeletePrinterAsync(id);
        if (!result)
            return NotFound();

        return NoContent();
    }

    [HttpPost("{id}/availability")]
    public async Task<ActionResult> SetAvailability(int id, [FromBody] bool isAvailable)
    {
        var result = await _printerService.SetPrinterAvailabilityAsync(id, isAvailable);
        if (!result)
            return NotFound();

        return Ok();
    }
}
