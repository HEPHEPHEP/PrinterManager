using PrinterManager.Client.Services;
using PrinterManager.Shared.DTOs;

namespace PrinterManager.Client;

public class Worker : BackgroundService
{
    private const int DefaultPollIntervalSeconds = 60;
    private const int MinimumPollIntervalSeconds = 5;

    private static readonly TimeSpan ErrorBackoff = TimeSpan.FromSeconds(30);

    /// <summary>Wartezeit, bis Windows einen frisch verbundenen Drucker kennt.</summary>
    private static readonly TimeSpan DefaultPrinterSettleDelay = TimeSpan.FromSeconds(2);

    private readonly ILogger<Worker> _logger;
    private readonly IPrinterDetectionService _printerDetection;
    private readonly IPrinterManagementService _printerManagement;
    private readonly IServerCommunicationService _serverCommunication;
    private readonly IAutostartService _autostartService;
    private readonly IConfiguration _configuration;
    private bool _isFirstRun = true;

    public Worker(
        ILogger<Worker> logger,
        IPrinterDetectionService printerDetection,
        IPrinterManagementService printerManagement,
        IServerCommunicationService serverCommunication,
        IAutostartService autostartService,
        IConfiguration configuration)
    {
        _logger = logger;
        _printerDetection = printerDetection;
        _printerManagement = printerManagement;
        _serverCommunication = serverCommunication;
        _autostartService = autostartService;
        _configuration = configuration;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("PrinterManager Client startet...");

        if (!_autostartService.IsAutostartEnabled())
        {
            _logger.LogInformation("Autostart nicht konfiguriert — wird für den aktuellen Benutzer aktiviert");
            _autostartService.EnableAutostart();
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = PollInterval;

            try
            {
                await RegisterAndProcessActionsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fehler im Worker-Durchlauf");
                delay = ErrorBackoff;
            }

            try
            {
                await Task.Delay(delay, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation("PrinterManager Client wird beendet");
    }

    /// <summary>
    /// Abfrageintervall aus der Konfiguration; zu kleine Werte würden zu einer
    /// Dauerschleife gegen den Server führen.
    /// </summary>
    private TimeSpan PollInterval
    {
        get
        {
            var seconds = _configuration.GetValue("PollIntervalSeconds", DefaultPollIntervalSeconds);

            if (seconds < MinimumPollIntervalSeconds)
            {
                _logger.LogWarning(
                    "PollIntervalSeconds={Configured} ist zu klein — es werden {Minimum} Sekunden verwendet",
                    seconds, MinimumPollIntervalSeconds);
                seconds = MinimumPollIntervalSeconds;
            }

            return TimeSpan.FromSeconds(seconds);
        }
    }

    private async Task RegisterAndProcessActionsAsync(CancellationToken cancellationToken)
    {
        var installedPrinters = _printerDetection.GetInstalledPrinters();
        _logger.LogDebug("{Count} installierte Drucker erkannt", installedPrinters.Count);

        var response = await _serverCommunication.RegisterAsync(installedPrinters, cancellationToken);
        if (response == null)
        {
            _logger.LogWarning("Registrierung beim Server fehlgeschlagen");
            return;
        }

        if (_isFirstRun)
        {
            _logger.LogInformation("Erfolgreich beim Server registriert");
            _isFirstRun = false;
        }

        if (response.Actions.Count > 0)
        {
            _logger.LogInformation("{Count} Aktionen vom Server erhalten", response.Actions.Count);
            await ProcessActionsAsync(response, cancellationToken);
        }
    }

    private async Task ProcessActionsAsync(PrinterActionsResponse response, CancellationToken cancellationToken)
    {
        foreach (var action in response.Actions)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                switch (action.Action)
                {
                    case PrinterAction.Install:
                        await _printerManagement.InstallPrinterAsync(action.SharePath ?? string.Empty, cancellationToken);
                        break;

                    case PrinterAction.Remove:
                        await _printerManagement.RemovePrinterAsync(action.PrinterName ?? string.Empty, cancellationToken);
                        break;

                    case PrinterAction.SetDefault:
                        // Der Drucker wurde eventuell im selben Durchlauf verbunden.
                        await Task.Delay(DefaultPrinterSettleDelay, cancellationToken);
                        await _printerManagement.SetDefaultPrinterAsync(
                            action.PrinterName ?? string.Empty, action.SharePath, cancellationToken);
                        break;

                    default:
                        _logger.LogWarning("Unbekannte Aktion {Action} für Drucker {PrinterName}",
                            action.Action, action.PrinterName);
                        break;
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fehler bei Aktion {Action} für Drucker {PrinterName}",
                    action.Action, action.PrinterName);
            }
        }
    }
}
