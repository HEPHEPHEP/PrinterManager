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
    /// <summary>Obergrenze für die Ersatzdrucker-Kette (Schutz vor Zyklen in Altdaten).</summary>
    private const int MaxReplacementDepth = 10;

    private readonly PrinterManagerDbContext _context;
    private readonly ILogger<ClientService> _logger;

    public ClientService(PrinterManagerDbContext context, ILogger<ClientService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<PrinterActionsResponse> RegisterClientAsync(ClientRegistrationDto dto)
    {
        _logger.LogDebug("Client meldet sich an: {Hostname}, Benutzer: {User}, Drucker: {Count}",
            dto.Hostname, dto.UserPrincipalName, dto.InstalledPrinters.Count);

        var client = await _context.Clients.FirstOrDefaultAsync(c => c.Hostname == dto.Hostname);

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

        await SyncInstalledPrintersAsync(client.Id, dto.InstalledPrinters);

        return await GetPrinterActionsAsync(dto.Hostname, dto.UserPrincipalName);
    }

    /// <summary>
    /// Gleicht die gemeldete Druckerliste mit der gespeicherten ab. Bestehende Einträge
    /// werden aktualisiert statt gelöscht und neu angelegt — sonst wächst die ID-Sequenz
    /// bei jedem Poll und <c>DetectedAt</c> verliert seine Aussage.
    /// </summary>
    private async Task SyncInstalledPrintersAsync(int clientId, List<InstalledPrinterDto> reported)
    {
        var stored = await _context.ClientPrinters
            .Where(cp => cp.ClientId == clientId)
            .ToListAsync();

        var managedByPath = await _context.Printers
            .Select(p => new { p.Id, p.SharePath })
            .ToListAsync();

        var pathLookup = managedByPath
            .GroupBy(p => p.SharePath, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().Id, StringComparer.OrdinalIgnoreCase);

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var printer in reported)
        {
            if (string.IsNullOrWhiteSpace(printer.PrinterName) || !seen.Add(printer.PrinterName))
                continue;

            int? managedPrinterId = null;
            if (!string.IsNullOrEmpty(printer.PrinterPath) &&
                pathLookup.TryGetValue(printer.PrinterPath, out var matchedId))
            {
                managedPrinterId = matchedId;
            }

            var existing = stored.FirstOrDefault(
                cp => string.Equals(cp.PrinterName, printer.PrinterName, StringComparison.OrdinalIgnoreCase));

            if (existing != null)
            {
                existing.PrinterPath = printer.PrinterPath;
                existing.IsDefault = printer.IsDefault;
                existing.ManagedPrinterId = managedPrinterId;
            }
            else
            {
                _context.ClientPrinters.Add(new ClientPrinter
                {
                    ClientId = clientId,
                    PrinterName = printer.PrinterName,
                    PrinterPath = printer.PrinterPath,
                    IsDefault = printer.IsDefault,
                    ManagedPrinterId = managedPrinterId
                });
            }
        }

        var removed = stored.Where(cp => !seen.Contains(cp.PrinterName)).ToList();
        if (removed.Count > 0)
        {
            _context.ClientPrinters.RemoveRange(removed);
        }

        await _context.SaveChangesAsync();
    }

    public async Task<PrinterActionsResponse> GetPrinterActionsAsync(string hostname, string userPrincipalName)
    {
        var client = await _context.Clients
            .Include(c => c.InstalledPrinters)
            .FirstOrDefaultAsync(c => c.Hostname == hostname);

        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.UserPrincipalName == userPrincipalName);

        if (client == null || user == null)
        {
            _logger.LogWarning("Client oder Benutzer unbekannt: Hostname={Hostname} gefunden={ClientFound}, " +
                "Benutzer={User} gefunden={UserFound}",
                hostname, client != null, userPrincipalName, user != null);
            return new PrinterActionsResponse();
        }

        var config = await GetOrCreateConfigAsync();

        var userAssignments = await _context.PrinterAssignments
            .Include(a => a.Printer)
            .Where(a => a.UserId == user.Id)
            .ToListAsync();

        var clientAssignments = await _context.PrinterAssignments
            .Include(a => a.Printer)
            .Where(a => a.ClientId == client.Id)
            .ToListAsync();

        var activeAssignments = config.AssignmentPriority == AssignmentPriority.UserPriority
            ? (userAssignments.Count > 0 ? userAssignments : clientAssignments)
            : (clientAssignments.Count > 0 ? clientAssignments : userAssignments);

        _logger.LogDebug(
            "{Hostname}/{User}: {UserCount} Benutzer-, {ClientCount} Client-Zuweisungen, " +
            "aktiv: {ActiveCount} (Priorität: {Priority})",
            hostname, userPrincipalName, userAssignments.Count, clientAssignments.Count,
            activeAssignments.Count, config.AssignmentPriority);

        Dictionary<int, Printer>? allPrinters = null;
        if (config.AutoAssignReplacementPrinters && activeAssignments.Any(a => a.Printer is { IsAvailable: false }))
        {
            allPrinters = await _context.Printers.ToDictionaryAsync(p => p.Id);
        }

        // Zuweisungen auf tatsächlich verfügbare Drucker auflösen (ggf. über Ersatzdrucker).
        var targetPrinters = new Dictionary<int, Printer>();
        Printer? defaultPrinter = null;

        foreach (var assignment in activeAssignments)
        {
            if (assignment.Printer == null)
                continue;

            var resolved = ResolveAvailablePrinter(assignment.Printer, allPrinters);
            if (resolved == null)
            {
                _logger.LogInformation(
                    "Drucker {Printer} ist nicht verfügbar und hat keinen verfügbaren Ersatz — übersprungen",
                    assignment.Printer.Name);
                continue;
            }

            targetPrinters[resolved.Id] = resolved;

            if (assignment.IsDefaultPrinter)
            {
                defaultPrinter = resolved;
            }
        }

        // UNC-Pfade sind unter Windows nicht case-sensitiv.
        var installedPaths = client.InstalledPrinters
            .Where(p => !string.IsNullOrEmpty(p.PrinterPath))
            .Select(p => p.PrinterPath!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var actions = new List<PrinterActionDto>();

        foreach (var printer in targetPrinters.Values)
        {
            if (!installedPaths.Contains(printer.SharePath))
            {
                actions.Add(new PrinterActionDto
                {
                    PrinterId = printer.Id,
                    Action = PrinterAction.Install,
                    SharePath = printer.SharePath,
                    PrinterName = printer.Name
                });
            }
        }

        foreach (var installedPrinter in client.InstalledPrinters)
        {
            if (installedPrinter.ManagedPrinterId.HasValue &&
                !targetPrinters.ContainsKey(installedPrinter.ManagedPrinterId.Value))
            {
                actions.Add(new PrinterActionDto
                {
                    PrinterId = installedPrinter.ManagedPrinterId.Value,
                    Action = PrinterAction.Remove,
                    SharePath = installedPrinter.PrinterPath,
                    PrinterName = installedPrinter.PrinterName
                });
            }
        }

        // Standarddrucker zuletzt, damit er nach Installation/Entfernung gesetzt wird.
        if (defaultPrinter != null)
        {
            actions.Add(new PrinterActionDto
            {
                PrinterId = defaultPrinter.Id,
                Action = PrinterAction.SetDefault,
                SharePath = defaultPrinter.SharePath,
                PrinterName = defaultPrinter.Name
            });
        }

        _logger.LogDebug("{Hostname}/{User}: {Count} Aktionen", hostname, userPrincipalName, actions.Count);

        return new PrinterActionsResponse { Actions = actions };
    }

    /// <summary>
    /// Liefert den Drucker selbst, wenn er verfügbar ist, sonst den ersten verfügbaren
    /// Ersatzdrucker in der Kette — oder <c>null</c>, wenn es keinen gibt.
    /// </summary>
    private static Printer? ResolveAvailablePrinter(Printer printer, Dictionary<int, Printer>? allPrinters)
    {
        if (printer.IsAvailable)
            return printer;

        if (allPrinters == null)
            return null;

        var visited = new HashSet<int> { printer.Id };
        var currentId = printer.ReplacementPrinterId;

        for (var depth = 0; depth < MaxReplacementDepth && currentId.HasValue; depth++)
        {
            if (!visited.Add(currentId.Value) || !allPrinters.TryGetValue(currentId.Value, out var candidate))
                return null;

            if (candidate.IsAvailable)
                return candidate;

            currentId = candidate.ReplacementPrinterId;
        }

        return null;
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
