using Microsoft.EntityFrameworkCore;
using PrinterManager.Server.Data;
using PrinterManager.Shared.DTOs;
using PrinterManager.Shared.Models;

namespace PrinterManager.Server.Services;

public interface IAssignmentService
{
    Task<List<AssignmentDto>> GetAllAssignmentsAsync();
    Task<List<AssignmentDto>> GetAssignmentsForUserAsync(int userId);
    Task<List<AssignmentDto>> GetAssignmentsForClientAsync(int clientId);
    Task<AssignmentDto> CreateAssignmentAsync(CreateAssignmentDto dto);
    Task<List<AssignmentDto>> CreateBulkAssignmentsAsync(BulkAssignmentDto dto);
    Task<bool> DeleteAssignmentAsync(int id);
}

public class AssignmentService : IAssignmentService
{
    private readonly PrinterManagerDbContext _context;

    public AssignmentService(PrinterManagerDbContext context)
    {
        _context = context;
    }

    public async Task<List<AssignmentDto>> GetAllAssignmentsAsync()
    {
        return await Project(_context.PrinterAssignments).ToListAsync();
    }

    public async Task<List<AssignmentDto>> GetAssignmentsForUserAsync(int userId)
    {
        return await Project(_context.PrinterAssignments.Where(a => a.UserId == userId)).ToListAsync();
    }

    public async Task<List<AssignmentDto>> GetAssignmentsForClientAsync(int clientId)
    {
        return await Project(_context.PrinterAssignments.Where(a => a.ClientId == clientId)).ToListAsync();
    }

    public async Task<AssignmentDto> CreateAssignmentAsync(CreateAssignmentDto dto)
    {
        var assignmentType = ParseAssignmentType(dto.AssignmentType);
        var targetId = GetTargetId(assignmentType, dto.UserId, dto.ClientId);

        await EnsurePrinterExistsAsync(dto.PrinterId);
        await EnsureTargetExistsAsync(assignmentType, targetId);

        var existing = await _context.PrinterAssignments
            .FirstOrDefaultAsync(a =>
                a.PrinterId == dto.PrinterId &&
                a.AssignmentType == assignmentType &&
                a.UserId == dto.UserId &&
                a.ClientId == dto.ClientId);

        if (dto.IsDefaultPrinter)
        {
            await ClearDefaultPrinterAsync(assignmentType, targetId);
        }

        if (existing != null)
        {
            // Zuweisung existiert bereits — nur das Standarddrucker-Flag nachziehen,
            // statt einen Duplikat-Eintrag anzulegen.
            existing.IsDefaultPrinter = dto.IsDefaultPrinter;
            await _context.SaveChangesAsync();
            return await LoadDtoAsync(existing.Id);
        }

        var assignment = new PrinterAssignment
        {
            PrinterId = dto.PrinterId,
            AssignmentType = assignmentType,
            UserId = assignmentType == AssignmentType.User ? targetId : null,
            ClientId = assignmentType == AssignmentType.Client ? targetId : null,
            IsDefaultPrinter = dto.IsDefaultPrinter
        };

        _context.PrinterAssignments.Add(assignment);
        await _context.SaveChangesAsync();

        return await LoadDtoAsync(assignment.Id);
    }

    public async Task<List<AssignmentDto>> CreateBulkAssignmentsAsync(BulkAssignmentDto dto)
    {
        var assignmentType = ParseAssignmentType(dto.AssignmentType);
        var targetIds = (assignmentType == AssignmentType.User ? dto.UserIds : dto.ClientIds)
            .Distinct()
            .ToList();
        var printerIds = dto.PrinterIds.Distinct().ToList();

        if (targetIds.Count == 0 || printerIds.Count == 0)
        {
            return new List<AssignmentDto>();
        }

        var knownPrinterIds = await _context.Printers
            .Where(p => printerIds.Contains(p.Id))
            .Select(p => p.Id)
            .ToListAsync();

        var unknownPrinter = printerIds.Except(knownPrinterIds).ToList();
        if (unknownPrinter.Count > 0)
        {
            throw new ArgumentException($"Unbekannte Drucker-IDs: {string.Join(", ", unknownPrinter)}.");
        }

        foreach (var targetId in targetIds)
        {
            await EnsureTargetExistsAsync(assignmentType, targetId);
        }

        if (dto.DefaultPrinterId.HasValue && !printerIds.Contains(dto.DefaultPrinterId.Value))
        {
            throw new ArgumentException("Der Standarddrucker muss Teil der Zuweisung sein.");
        }

        // Bestehende Zuweisungen einmal laden statt pro Drucker/Ziel zu fragen.
        var existingQuery = _context.PrinterAssignments.Where(a => a.AssignmentType == assignmentType);
        existingQuery = assignmentType == AssignmentType.User
            ? existingQuery.Where(a => a.UserId != null && targetIds.Contains(a.UserId.Value))
            : existingQuery.Where(a => a.ClientId != null && targetIds.Contains(a.ClientId.Value));

        var existing = await existingQuery.ToListAsync();

        var created = new List<PrinterAssignment>();

        foreach (var targetId in targetIds)
        {
            var existingForTarget = existing
                .Where(a => (assignmentType == AssignmentType.User ? a.UserId : a.ClientId) == targetId)
                .ToList();

            if (dto.DefaultPrinterId.HasValue)
            {
                foreach (var previousDefault in existingForTarget.Where(a => a.IsDefaultPrinter))
                {
                    previousDefault.IsDefaultPrinter = false;
                }
            }

            foreach (var printerId in printerIds)
            {
                var isDefault = dto.DefaultPrinterId == printerId;
                var duplicate = existingForTarget.FirstOrDefault(a => a.PrinterId == printerId);

                if (duplicate != null)
                {
                    duplicate.IsDefaultPrinter = isDefault || duplicate.IsDefaultPrinter;
                    continue;
                }

                var assignment = new PrinterAssignment
                {
                    PrinterId = printerId,
                    AssignmentType = assignmentType,
                    UserId = assignmentType == AssignmentType.User ? targetId : null,
                    ClientId = assignmentType == AssignmentType.Client ? targetId : null,
                    IsDefaultPrinter = isDefault
                };

                _context.PrinterAssignments.Add(assignment);
                created.Add(assignment);
            }
        }

        await _context.SaveChangesAsync();

        var createdIds = created.Select(a => a.Id).ToList();
        return await Project(_context.PrinterAssignments.Where(a => createdIds.Contains(a.Id))).ToListAsync();
    }

    public async Task<bool> DeleteAssignmentAsync(int id)
    {
        var assignment = await _context.PrinterAssignments.FindAsync(id);
        if (assignment == null)
            return false;

        _context.PrinterAssignments.Remove(assignment);
        await _context.SaveChangesAsync();
        return true;
    }

    private async Task ClearDefaultPrinterAsync(AssignmentType assignmentType, int targetId)
    {
        var query = _context.PrinterAssignments
            .Where(a => a.IsDefaultPrinter && a.AssignmentType == assignmentType);

        query = assignmentType == AssignmentType.User
            ? query.Where(a => a.UserId == targetId)
            : query.Where(a => a.ClientId == targetId);

        var existingDefaults = await query.ToListAsync();

        foreach (var defaultAssignment in existingDefaults)
        {
            defaultAssignment.IsDefaultPrinter = false;
        }
    }

    private async Task<AssignmentDto> LoadDtoAsync(int id)
    {
        return await Project(_context.PrinterAssignments.Where(a => a.Id == id)).FirstAsync();
    }

    private static IQueryable<AssignmentDto> Project(IQueryable<PrinterAssignment> query)
    {
        return query.Select(a => new AssignmentDto
        {
            Id = a.Id,
            PrinterId = a.PrinterId,
            PrinterName = a.Printer!.Name,
            ServiceNumber = a.Printer!.ServiceNumber,
            AssignmentType = a.AssignmentType.ToString(),
            UserId = a.UserId,
            UserPrincipalName = a.User != null ? a.User.UserPrincipalName : null,
            ClientId = a.ClientId,
            ClientHostname = a.Client != null ? a.Client.Hostname : null,
            IsDefaultPrinter = a.IsDefaultPrinter
        });
    }

    private async Task EnsurePrinterExistsAsync(int printerId)
    {
        if (!await _context.Printers.AnyAsync(p => p.Id == printerId))
        {
            throw new ArgumentException($"Drucker {printerId} existiert nicht.");
        }
    }

    private async Task EnsureTargetExistsAsync(AssignmentType assignmentType, int targetId)
    {
        var exists = assignmentType == AssignmentType.User
            ? await _context.Users.AnyAsync(u => u.Id == targetId)
            : await _context.Clients.AnyAsync(c => c.Id == targetId);

        if (!exists)
        {
            throw new ArgumentException(
                assignmentType == AssignmentType.User
                    ? $"Benutzer {targetId} existiert nicht."
                    : $"Client {targetId} existiert nicht.");
        }
    }

    private static AssignmentType ParseAssignmentType(string? value)
    {
        if (!Enum.TryParse<AssignmentType>(value, ignoreCase: true, out var parsed) || !Enum.IsDefined(parsed))
        {
            throw new ArgumentException(
                $"Ungültiger Zuweisungstyp '{value}'. Erlaubt: {string.Join(", ", Enum.GetNames<AssignmentType>())}.");
        }

        return parsed;
    }

    /// <summary>
    /// Stellt sicher, dass genau die zum Typ passende Ziel-ID gesetzt ist. Ohne diese Prüfung
    /// schlägt erst die DB-Check-Constraint zu (HTTP 500) oder es entstehen Zuweisungen
    /// ohne Ziel.
    /// </summary>
    private static int GetTargetId(AssignmentType assignmentType, int? userId, int? clientId)
    {
        if (assignmentType == AssignmentType.User)
        {
            if (userId is null or <= 0)
                throw new ArgumentException("Für eine Benutzer-Zuweisung ist UserId erforderlich.");
            if (clientId.HasValue)
                throw new ArgumentException("Für eine Benutzer-Zuweisung darf ClientId nicht gesetzt sein.");
            return userId.Value;
        }

        if (clientId is null or <= 0)
            throw new ArgumentException("Für eine Client-Zuweisung ist ClientId erforderlich.");
        if (userId.HasValue)
            throw new ArgumentException("Für eine Client-Zuweisung darf UserId nicht gesetzt sein.");
        return clientId.Value;
    }
}
