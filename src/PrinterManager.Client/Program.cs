using PrinterManager.Client;
using PrinterManager.Client.Services;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "PrinterManager Client";
});

builder.Services.AddSingleton<IUserSessionService, UserSessionService>();
builder.Services.AddSingleton<IPrinterDetectionService, PrinterDetectionService>();
builder.Services.AddSingleton<IPrinterManagementService, PrinterManagementService>();
builder.Services.AddSingleton<IServerCommunicationService, ServerCommunicationService>();
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
