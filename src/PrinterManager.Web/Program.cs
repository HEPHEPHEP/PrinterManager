using PrinterManager.Web.Components;
using PrinterManager.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// WICHTIG: Scoped, nicht Singleton! In Blazor Server entspricht "Scoped" genau einer
// Benutzer-Verbindung (Circuit). Als Singleton würden sich ALLE Besucher Token, Benutzername
// und Rolle teilen — wer sich anmeldet, meldet damit alle anderen mit an.
builder.Services.AddScoped<AuthStateService>();

builder.Services.AddHttpClient<IApiService, ApiService>((serviceProvider, client) =>
{
    var config = serviceProvider.GetRequiredService<IConfiguration>();
    client.BaseAddress = new Uri(config["ApiUrl"] ?? "http://localhost:5000");
    client.Timeout = TimeSpan.FromSeconds(30);
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
