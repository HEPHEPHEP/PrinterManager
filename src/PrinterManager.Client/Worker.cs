using PrinterManager.Client.Services;
using PrinterManager.Shared.DTOs;

namespace PrinterManager.Client;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly IPrinterDetectionService _printerDetection;
    private readonly IPrinterManagementService _printerManagement;
    private readonly IServerCommunicationService _serverCommunication;
    private readonly IConfiguration _configuration;
    private bool _isFirstRun = true;

    public Worker(
        ILogger<Worker> logger,
        IPrinterDetectionService printerDetection,
        IPrinterManagementService printerManagement,
        IServerCommunicationService serverCommunication,
        IConfiguration configuration)
    {
        _logger = logger;
        _printerDetection = printerDetection;
        _printerManagement = printerManagement;
        _serverCommunication = serverCommunication;
        _configuration = configuration;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("PrinterManager Client starting...");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (_isFirstRun)
                {
                    await InitialRegistrationAsync();
                    _isFirstRun = false;
                }
                else
                {
                    await CheckForActionsAsync();
                }

                var pollInterval = _configuration.GetValue<int>("PollIntervalSeconds", 60);
                await Task.Delay(TimeSpan.FromSeconds(pollInterval), stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in worker loop");
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
        }
    }

    private async Task InitialRegistrationAsync()
    {
        _logger.LogInformation("Performing initial registration...");

        var installedPrinters = _printerDetection.GetInstalledPrinters();
        _logger.LogInformation($"Found {installedPrinters.Count} installed printers");

        var response = await _serverCommunication.RegisterAsync(installedPrinters);
        if (response != null)
        {
            _logger.LogInformation("Successfully registered with server");
            await ProcessActionsAsync(response);
        }
        else
        {
            _logger.LogWarning("Failed to register with server");
        }
    }

    private async Task CheckForActionsAsync()
    {
        var response = await _serverCommunication.GetActionsAsync();
        if (response != null && response.Actions.Any())
        {
            _logger.LogInformation($"Received {response.Actions.Count} actions from server");
            await ProcessActionsAsync(response);
        }
    }

    private async Task ProcessActionsAsync(PrinterActionsResponse response)
    {
        foreach (var action in response.Actions)
        {
            try
            {
                switch (action.Action)
                {
                    case PrinterAction.Install:
                        _logger.LogInformation($"Installing printer: {action.PrinterName} ({action.SharePath})");
                        await _printerManagement.InstallPrinterAsync(action.SharePath!);
                        break;

                    case PrinterAction.Remove:
                        _logger.LogInformation($"Removing printer: {action.PrinterName}");
                        await _printerManagement.RemovePrinterAsync(action.PrinterName!);
                        break;

                    case PrinterAction.SetDefault:
                        _logger.LogInformation($"Setting default printer: {action.PrinterName}");
                        // Give printer time to install before setting as default
                        await Task.Delay(2000);
                        await _printerManagement.SetDefaultPrinterAsync(action.PrinterName!);
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error processing action {action.Action} for printer {action.PrinterName}");
            }
        }
    }
}
