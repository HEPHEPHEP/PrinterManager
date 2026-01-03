using Microsoft.EntityFrameworkCore;
using PrinterManager.Server.Data;
using PrinterManager.Shared.DTOs;
using PrinterManager.Shared.Models;

namespace PrinterManager.Server.Services;

public interface IPrinterService
{
    Task<List<PrinterDto>> GetAllPrintersAsync();
    Task<PrinterDto?> GetPrinterByIdAsync(int id);
    Task<PrinterDto> CreatePrinterAsync(CreatePrinterDto dto);
    Task<PrinterDto?> UpdatePrinterAsync(int id, UpdatePrinterDto dto);
    Task<bool> DeletePrinterAsync(int id);
    Task<bool> SetPrinterAvailabilityAsync(int id, bool isAvailable);
}

public class PrinterService : IPrinterService
{
    private readonly PrinterManagerDbContext _context;
    private readonly IAssignmentService _assignmentService;

    public PrinterService(PrinterManagerDbContext context, IAssignmentService assignmentService)
    {
        _context = context;
        _assignmentService = assignmentService;
    }

    public async Task<List<PrinterDto>> GetAllPrintersAsync()
    {
        return await _context.Printers
            .Include(p => p.ReplacementPrinter)
            .Select(p => new PrinterDto
            {
                Id = p.Id,
                PrinterId = p.PrinterId,
                Name = p.Name,
                ServiceNumber = p.ServiceNumber,
                SharePath = p.SharePath,
                Description = p.Description,
                Location = p.Location,
                ServerName = p.ServerName,
                IsAvailable = p.IsAvailable,
                ReplacementPrinterId = p.ReplacementPrinterId,
                ReplacementPrinterName = p.ReplacementPrinter != null ? p.ReplacementPrinter.Name : null
            })
            .ToListAsync();
    }

    public async Task<PrinterDto?> GetPrinterByIdAsync(int id)
    {
        var printer = await _context.Printers
            .Include(p => p.ReplacementPrinter)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (printer == null)
            return null;

        return new PrinterDto
        {
            Id = printer.Id,
            PrinterId = printer.PrinterId,
            Name = printer.Name,
            ServiceNumber = printer.ServiceNumber,
            SharePath = printer.SharePath,
            Description = printer.Description,
            Location = printer.Location,
            ServerName = printer.ServerName,
            IsAvailable = printer.IsAvailable,
            ReplacementPrinterId = printer.ReplacementPrinterId,
            ReplacementPrinterName = printer.ReplacementPrinter?.Name
        };
    }

    public async Task<PrinterDto> CreatePrinterAsync(CreatePrinterDto dto)
    {
        var printer = new Printer
        {
            PrinterId = dto.PrinterId,
            Name = dto.Name,
            ServiceNumber = dto.ServiceNumber,
            SharePath = dto.SharePath,
            Description = dto.Description,
            Location = dto.Location,
            ServerName = dto.ServerName,
            IsAvailable = true
        };

        _context.Printers.Add(printer);
        await _context.SaveChangesAsync();

        return new PrinterDto
        {
            Id = printer.Id,
            PrinterId = printer.PrinterId,
            Name = printer.Name,
            ServiceNumber = printer.ServiceNumber,
            SharePath = printer.SharePath,
            Description = printer.Description,
            Location = printer.Location,
            ServerName = printer.ServerName,
            IsAvailable = printer.IsAvailable
        };
    }

    public async Task<PrinterDto?> UpdatePrinterAsync(int id, UpdatePrinterDto dto)
    {
        var printer = await _context.Printers
            .Include(p => p.ReplacementPrinter)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (printer == null)
            return null;

        if (dto.Name != null)
            printer.Name = dto.Name;
        if (dto.ServiceNumber != null)
            printer.ServiceNumber = dto.ServiceNumber;
        if (dto.Description != null)
            printer.Description = dto.Description;
        if (dto.Location != null)
            printer.Location = dto.Location;
        if (dto.ReplacementPrinterId.HasValue)
            printer.ReplacementPrinterId = dto.ReplacementPrinterId.Value == 0 ? null : dto.ReplacementPrinterId.Value;

        // Handle availability change
        if (dto.IsAvailable.HasValue && dto.IsAvailable.Value != printer.IsAvailable)
        {
            await SetPrinterAvailabilityAsync(id, dto.IsAvailable.Value);
            return await GetPrinterByIdAsync(id);
        }

        printer.LastModified = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return await GetPrinterByIdAsync(id);
    }

    public async Task<bool> DeletePrinterAsync(int id)
    {
        var printer = await _context.Printers.FindAsync(id);
        if (printer == null)
            return false;

        _context.Printers.Remove(printer);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> SetPrinterAvailabilityAsync(int id, bool isAvailable)
    {
        var printer = await _context.Printers
            .Include(p => p.ReplacementPrinter)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (printer == null)
            return false;

        printer.IsAvailable = isAvailable;
        printer.LastModified = DateTime.UtcNow;

        // If marking as unavailable and replacement printer exists, trigger replacement
        if (!isAvailable && printer.ReplacementPrinterId.HasValue)
        {
            await _assignmentService.ApplyReplacementPrinterAsync(id, printer.ReplacementPrinterId.Value);
        }
        // If marking as available again, restore original assignments
        else if (isAvailable && printer.ReplacementPrinterId.HasValue)
        {
            await _assignmentService.RestoreOriginalPrinterAsync(id);
        }

        await _context.SaveChangesAsync();
        return true;
    }
}
