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

    public PrinterService(PrinterManagerDbContext context)
    {
        _context = context;
    }

    public async Task<List<PrinterDto>> GetAllPrintersAsync()
    {
        return await Project(_context.Printers).ToListAsync();
    }

    public async Task<PrinterDto?> GetPrinterByIdAsync(int id)
    {
        return await Project(_context.Printers.Where(p => p.Id == id)).FirstOrDefaultAsync();
    }

    public async Task<PrinterDto> CreatePrinterAsync(CreatePrinterDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.PrinterId))
            throw new ArgumentException("Drucker-ID ist erforderlich.");
        if (string.IsNullOrWhiteSpace(dto.Name))
            throw new ArgumentException("Name ist erforderlich.");
        if (string.IsNullOrWhiteSpace(dto.SharePath))
            throw new ArgumentException("Freigabepfad ist erforderlich.");

        if (await _context.Printers.AnyAsync(p => p.PrinterId == dto.PrinterId))
            throw new ArgumentException($"Drucker-ID '{dto.PrinterId}' ist bereits vergeben.");

        var printer = new Printer
        {
            PrinterId = dto.PrinterId.Trim(),
            Name = dto.Name.Trim(),
            ServiceNumber = dto.ServiceNumber,
            SharePath = dto.SharePath.Trim(),
            Description = dto.Description,
            Location = dto.Location,
            ServerName = dto.ServerName,
            IsAvailable = true
        };

        _context.Printers.Add(printer);
        await _context.SaveChangesAsync();

        return (await GetPrinterByIdAsync(printer.Id))!;
    }

    public async Task<PrinterDto?> UpdatePrinterAsync(int id, UpdatePrinterDto dto)
    {
        var printer = await _context.Printers.FirstOrDefaultAsync(p => p.Id == id);

        if (printer == null)
            return null;

        if (dto.Name != null)
        {
            if (string.IsNullOrWhiteSpace(dto.Name))
                throw new ArgumentException("Name darf nicht leer sein.");
            printer.Name = dto.Name.Trim();
        }
        if (dto.ServiceNumber != null)
            printer.ServiceNumber = dto.ServiceNumber;
        if (dto.Description != null)
            printer.Description = dto.Description;
        if (dto.Location != null)
            printer.Location = dto.Location;

        if (dto.ReplacementPrinterId.HasValue)
        {
            var replacementId = dto.ReplacementPrinterId.Value == 0 ? (int?)null : dto.ReplacementPrinterId.Value;
            await ValidateReplacementAsync(printer.Id, replacementId);
            printer.ReplacementPrinterId = replacementId;
        }

        if (dto.IsAvailable.HasValue)
            printer.IsAvailable = dto.IsAvailable.Value;

        printer.LastModified = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return await GetPrinterByIdAsync(id);
    }

    public async Task<bool> DeletePrinterAsync(int id)
    {
        var printer = await _context.Printers.FindAsync(id);
        if (printer == null)
            return false;

        // ClientPrinters kennen keine Fremdschlüssel-Beziehung zu Printers — die
        // Verweise müssen von Hand gelöst werden, sonst zeigen sie ins Leere.
        var staleReferences = await _context.ClientPrinters
            .Where(cp => cp.ManagedPrinterId == id)
            .ToListAsync();

        foreach (var reference in staleReferences)
        {
            reference.ManagedPrinterId = null;
        }

        _context.Printers.Remove(printer);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> SetPrinterAvailabilityAsync(int id, bool isAvailable)
    {
        var printer = await _context.Printers.FirstOrDefaultAsync(p => p.Id == id);

        if (printer == null)
            return false;

        printer.IsAvailable = isAvailable;
        printer.LastModified = DateTime.UtcNow;

        // Ersatzdrucker werden nicht mehr als zusätzliche Zuweisungen in die Datenbank
        // geschrieben, sondern beim Abruf der Client-Aktionen aufgelöst
        // (siehe ClientService.GetPrinterActionsAsync). Dadurch bleiben die echten
        // Zuweisungen unangetastet.
        await _context.SaveChangesAsync();
        return true;
    }

    /// <summary>
    /// Verhindert Selbstreferenzen und Zyklen in der Ersatzdrucker-Kette.
    /// </summary>
    private async Task ValidateReplacementAsync(int printerId, int? replacementId)
    {
        if (replacementId is null)
            return;

        if (replacementId == printerId)
            throw new ArgumentException("Ein Drucker kann nicht sein eigener Ersatzdrucker sein.");

        var chain = await _context.Printers
            .Select(p => new { p.Id, p.ReplacementPrinterId })
            .ToDictionaryAsync(p => p.Id, p => p.ReplacementPrinterId);

        if (!chain.ContainsKey(replacementId.Value))
            throw new ArgumentException($"Ersatzdrucker {replacementId} existiert nicht.");

        var current = replacementId;
        var visited = new HashSet<int> { printerId };

        while (current.HasValue)
        {
            if (!visited.Add(current.Value))
                throw new ArgumentException("Die Ersatzdrucker-Kette darf keinen Zyklus bilden.");

            current = chain.TryGetValue(current.Value, out var next) ? next : null;
        }
    }

    private static IQueryable<PrinterDto> Project(IQueryable<Printer> query)
    {
        return query.Select(p => new PrinterDto
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
        });
    }
}
