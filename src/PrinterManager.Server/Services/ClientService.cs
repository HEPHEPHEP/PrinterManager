using Microsoft.EntityFrameworkCore;
using PrinterManager.Server.Data;
using PrinterManager.Shared.DTOs;
using PrinterManager.Shared.Models;

namespace PrinterManager.Server.Services;

public interface IClientService
{
    Task<PrinterActionsResponse> RegisterClientAsync(ClientRegistrationDto dto);
    Task<PrinterActionsResponse> GetPrinterActionsAsync(string hostname, string userPrincipalName);
}

public class ClientService : IClientService
{
    private readonly PrinterManagerDbContext _context;

    public ClientService(PrinterManagerDbContext context)
    {
        _context = context;
    }

    public async Task<PrinterActionsResponse> RegisterClientAsync(ClientRegistrationDto dto)
    {
        // Find or create client
        var client = await _context.Clients
            .Include(c => c.InstalledPrinters)
            .FirstOrDefaultAsync(c => c.Hostname == dto.Hostname);

        if (client == null)
        {
            client = new Client
            {
                Hostname = dto.Hostname,
                IpAddress = dto.IpAddress,
                OperatingSystem = dto.OperatingSystem,
                IsActive = true
            };
            _context.Clients.Add(client);
        }
        else
        {
            client.LastSeen = DateTime.UtcNow;
            client.IpAddress = dto.IpAddress;
            client.OperatingSystem = dto.OperatingSystem;
            client.IsActive = true;
        }

        // Find or create user
        var user = await _context.Users.FirstOrDefaultAsync(u => u.UserPrincipalName == dto.UserPrincipalName);
        if (user == null)
        {
            user = new User
            {
                UserPrincipalName = dto.UserPrincipalName,
                IsActive = true
            };
            _context.Users.Add(user);
        }
        else
        {
            user.LastSeen = DateTime.UtcNow;
            user.IsActive = true;
        }

        await _context.SaveChangesAsync();

        // Update installed printers list
        // Remove old entries
        _context.ClientPrinters.RemoveRange(client.InstalledPrinters);

        // Add current printers from client
        foreach (var installedPrinter in dto.InstalledPrinters)
        {
            // Try to match with managed printer by SharePath
            var managedPrinter = await _context.Printers
                .FirstOrDefaultAsync(p => p.SharePath == installedPrinter.PrinterPath);

            var clientPrinter = new ClientPrinter
            {
                ClientId = client.Id,
                PrinterName = installedPrinter.PrinterName,
                PrinterPath = installedPrinter.PrinterPath,
                IsDefault = installedPrinter.IsDefault,
                ManagedPrinterId = managedPrinter?.Id
            };
            _context.ClientPrinters.Add(clientPrinter);
        }
        await _context.SaveChangesAsync();

        // Return printer actions
        return await GetPrinterActionsAsync(dto.Hostname, dto.UserPrincipalName);
    }

    public async Task<PrinterActionsResponse> GetPrinterActionsAsync(string hostname, string userPrincipalName)
    {
        var client = await _context.Clients
            .Include(c => c.InstalledPrinters)
            .FirstOrDefaultAsync(c => c.Hostname == hostname);

        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.UserPrincipalName == userPrincipalName);

        if (client == null || user == null)
            return new PrinterActionsResponse();

        var config = await _context.SystemConfigurations.FirstAsync();

        // Get user and client assignments
        var userAssignments = await _context.PrinterAssignments
            .Include(a => a.Printer)
            .Where(a => a.UserId == user.Id && a.Printer!.IsAvailable)
            .ToListAsync();

        var clientAssignments = await _context.PrinterAssignments
            .Include(a => a.Printer)
            .Where(a => a.ClientId == client.Id && a.Printer!.IsAvailable)
            .ToListAsync();

        // Determine which assignments to use based on priority
        List<PrinterAssignment> activeAssignments;
        if (config.AssignmentPriority == AssignmentPriority.UserPriority)
        {
            activeAssignments = userAssignments.Any() ? userAssignments : clientAssignments;
        }
        else
        {
            activeAssignments = clientAssignments.Any() ? clientAssignments : userAssignments;
        }

        var actions = new List<PrinterActionDto>();
        var installedPrinterPaths = client.InstalledPrinters.Select(p => p.PrinterPath).ToHashSet();

        // Install assigned printers
        foreach (var assignment in activeAssignments)
        {
            if (!installedPrinterPaths.Contains(assignment.Printer!.SharePath))
            {
                actions.Add(new PrinterActionDto
                {
                    PrinterId = assignment.PrinterId,
                    Action = PrinterAction.Install,
                    SharePath = assignment.Printer.SharePath,
                    PrinterName = assignment.Printer.Name
                });
            }

            // Set default printer
            if (assignment.IsDefaultPrinter)
            {
                actions.Add(new PrinterActionDto
                {
                    PrinterId = assignment.PrinterId,
                    Action = PrinterAction.SetDefault,
                    SharePath = assignment.Printer.SharePath,
                    PrinterName = assignment.Printer.Name
                });
            }
        }

        // Remove printers that are no longer assigned
        var assignedPrinterIds = activeAssignments.Select(a => a.PrinterId).ToHashSet();
        var managedPrinters = await _context.Printers
            .Where(p => assignedPrinterIds.Contains(p.Id))
            .ToListAsync();

        var assignedPaths = managedPrinters.Select(p => p.SharePath).ToHashSet();

        foreach (var installedPrinter in client.InstalledPrinters)
        {
            if (installedPrinter.ManagedPrinterId.HasValue &&
                !assignedPrinterIds.Contains(installedPrinter.ManagedPrinterId.Value))
            {
                actions.Add(new PrinterActionDto
                {
                    PrinterId = installedPrinter.ManagedPrinterId.Value,
                    Action = PrinterAction.Remove,
                    PrinterName = installedPrinter.PrinterName
                });
            }
        }

        return new PrinterActionsResponse
        {
            Actions = actions
        };
    }
}
