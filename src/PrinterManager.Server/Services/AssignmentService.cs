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
    Task<bool> DeleteAssignmentAsync(int id);
    Task ApplyReplacementPrinterAsync(int originalPrinterId, int replacementPrinterId);
    Task RestoreOriginalPrinterAsync(int originalPrinterId);
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
        return await _context.PrinterAssignments
            .Include(a => a.Printer)
            .Include(a => a.User)
            .Include(a => a.Client)
            .Select(a => new AssignmentDto
            {
                Id = a.Id,
                PrinterId = a.PrinterId,
                PrinterName = a.Printer!.Name,
                AssignmentType = a.AssignmentType.ToString(),
                UserId = a.UserId,
                UserPrincipalName = a.User != null ? a.User.UserPrincipalName : null,
                ClientId = a.ClientId,
                ClientHostname = a.Client != null ? a.Client.Hostname : null,
                IsDefaultPrinter = a.IsDefaultPrinter
            })
            .ToListAsync();
    }

    public async Task<List<AssignmentDto>> GetAssignmentsForUserAsync(int userId)
    {
        return await _context.PrinterAssignments
            .Include(a => a.Printer)
            .Where(a => a.UserId == userId)
            .Select(a => new AssignmentDto
            {
                Id = a.Id,
                PrinterId = a.PrinterId,
                PrinterName = a.Printer!.Name,
                AssignmentType = a.AssignmentType.ToString(),
                UserId = a.UserId,
                IsDefaultPrinter = a.IsDefaultPrinter
            })
            .ToListAsync();
    }

    public async Task<List<AssignmentDto>> GetAssignmentsForClientAsync(int clientId)
    {
        return await _context.PrinterAssignments
            .Include(a => a.Printer)
            .Where(a => a.ClientId == clientId)
            .Select(a => new AssignmentDto
            {
                Id = a.Id,
                PrinterId = a.PrinterId,
                PrinterName = a.Printer!.Name,
                AssignmentType = a.AssignmentType.ToString(),
                ClientId = a.ClientId,
                IsDefaultPrinter = a.IsDefaultPrinter
            })
            .ToListAsync();
    }

    public async Task<AssignmentDto> CreateAssignmentAsync(CreateAssignmentDto dto)
    {
        var assignmentType = Enum.Parse<AssignmentType>(dto.AssignmentType);

        var assignment = new PrinterAssignment
        {
            PrinterId = dto.PrinterId,
            AssignmentType = assignmentType,
            UserId = dto.UserId,
            ClientId = dto.ClientId,
            IsDefaultPrinter = dto.IsDefaultPrinter
        };

        _context.PrinterAssignments.Add(assignment);
        await _context.SaveChangesAsync();

        var created = await _context.PrinterAssignments
            .Include(a => a.Printer)
            .Include(a => a.User)
            .Include(a => a.Client)
            .FirstAsync(a => a.Id == assignment.Id);

        return new AssignmentDto
        {
            Id = created.Id,
            PrinterId = created.PrinterId,
            PrinterName = created.Printer!.Name,
            AssignmentType = created.AssignmentType.ToString(),
            UserId = created.UserId,
            UserPrincipalName = created.User?.UserPrincipalName,
            ClientId = created.ClientId,
            ClientHostname = created.Client?.Hostname,
            IsDefaultPrinter = created.IsDefaultPrinter
        };
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

    public async Task ApplyReplacementPrinterAsync(int originalPrinterId, int replacementPrinterId)
    {
        // Get all assignments for the original printer
        var assignments = await _context.PrinterAssignments
            .Where(a => a.PrinterId == originalPrinterId)
            .ToListAsync();

        // Create temporary assignments for the replacement printer
        foreach (var assignment in assignments)
        {
            var replacementAssignment = new PrinterAssignment
            {
                PrinterId = replacementPrinterId,
                AssignmentType = assignment.AssignmentType,
                UserId = assignment.UserId,
                ClientId = assignment.ClientId,
                IsDefaultPrinter = assignment.IsDefaultPrinter
            };

            _context.PrinterAssignments.Add(replacementAssignment);
        }

        await _context.SaveChangesAsync();
    }

    public async Task RestoreOriginalPrinterAsync(int originalPrinterId)
    {
        var printer = await _context.Printers
            .Include(p => p.ReplacementPrinter)
            .FirstOrDefaultAsync(p => p.Id == originalPrinterId);

        if (printer?.ReplacementPrinterId == null)
            return;

        // Remove replacement printer assignments that were created for this printer
        var replacementAssignments = await _context.PrinterAssignments
            .Where(a => a.PrinterId == printer.ReplacementPrinterId.Value)
            .ToListAsync();

        // Only remove those that have corresponding assignments for the original printer
        var originalAssignments = await _context.PrinterAssignments
            .Where(a => a.PrinterId == originalPrinterId)
            .ToListAsync();

        foreach (var replacement in replacementAssignments)
        {
            var hasOriginal = originalAssignments.Any(o =>
                o.AssignmentType == replacement.AssignmentType &&
                o.UserId == replacement.UserId &&
                o.ClientId == replacement.ClientId);

            if (hasOriginal)
            {
                _context.PrinterAssignments.Remove(replacement);
            }
        }

        await _context.SaveChangesAsync();
    }
}
