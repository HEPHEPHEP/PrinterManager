using PrinterManager.Client;
using PrinterManager.Client.Services;

var builder = Host.CreateApplicationBuilder(args);
var configuration = builder.Configuration;

builder.Services.AddSingleton<IUserSessionService, UserSessionService>();
builder.Services.AddSingleton<IPrinterDetectionService, PrinterDetectionService>();
builder.Services.AddSingleton<IPrinterManagementService, PrinterManagementService>();
builder.Services.AddSingleton<IAutostartService, AutostartService>();

// Typisierter HttpClient statt einer selbst erzeugten Instanz: die Verbindungen werden
// vom Factory-Handler verwaltet und DNS-Änderungen schlagen durch.
builder.Services.AddHttpClient<IServerCommunicationService, ServerCommunicationService>(client =>
{
    var serverUrl = configuration["ServerUrl"] ?? "http://localhost:5000";
    client.BaseAddress = new Uri(serverUrl);
    client.Timeout = TimeSpan.FromSeconds(30);

    // Gemeinsamer Schlüssel für die Client-Endpunkte (Server: "ClientApi:Key").
    var clientApiKey = configuration["ClientApiKey"];
    if (!string.IsNullOrEmpty(clientApiKey))
    {
        client.DefaultRequestHeaders.Add(ServerCommunicationService.ClientKeyHeader, clientApiKey);
    }
});

builder.Services.AddHostedService<Worker>();

var host = builder.Build();

var logger = host.Services.GetRequiredService<ILogger<Program>>();
using (var currentProcess = System.Diagnostics.Process.GetCurrentProcess())
{
    logger.LogInformation(
        "PrinterManager Client startet als Benutzeranwendung (kein Windows-Dienst). " +
        "Benutzer: {User}, Windows-Session: {SessionId}",
        Environment.UserName,
        currentProcess.SessionId);
}

if (string.IsNullOrEmpty(configuration["ClientApiKey"]))
{
    logger.LogInformation(
        "Kein ClientApiKey gesetzt — das genügt, solange der Server \"ClientApi:RequireKey\" " +
        "nicht aktiviert hat. Der Server nennt den Schlüssel in seiner Startausgabe.");
}

host.Run();
