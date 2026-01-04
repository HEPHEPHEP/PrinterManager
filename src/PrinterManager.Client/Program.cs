using PrinterManager.Client;
using PrinterManager.Client.Services;

var builder = Host.CreateApplicationBuilder(args);

// Configure services
builder.Services.AddSingleton<IUserSessionService, UserSessionService>();
builder.Services.AddSingleton<IPrinterDetectionService, PrinterDetectionService>();
builder.Services.AddSingleton<IPrinterManagementService, PrinterManagementService>();
builder.Services.AddSingleton<IServerCommunicationService, ServerCommunicationService>();
builder.Services.AddSingleton<IAutostartService, AutostartService>();
builder.Services.AddHostedService<Worker>();

var host = builder.Build();

// Log that we're running as a normal application, not a service
var logger = host.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("PrinterManager Client starting as user application (not a Windows Service)");
logger.LogInformation($"Running as user: {Environment.UserName}");
logger.LogInformation($"Session ID: {System.Diagnostics.Process.GetCurrentProcess().SessionId}");

host.Run();
