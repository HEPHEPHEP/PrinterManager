using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using PrinterManager.Shared.Http;
using PrinterManager.Web.Components;
using PrinterManager.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Anmeldung per Cookie. Das Cookie trägt Name, Rolle und das JWT des Servers (siehe
// Login.razor) und ist per Data Protection verschlüsselt. Anders als ein Zustand im
// Arbeitsspeicher sieht es jede Anfrage: das Vorab-Rendern, die interaktive Verbindung und
// ein Neuladen der Seite.
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "PrinterManager.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        // Die Web-Anwendung läuft standardmäßig über HTTP — "Always" würde dort das Cookie
        // verwerfen. Hinter HTTPS wird es automatisch als Secure gesetzt.
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.LoginPath = "/login";
        options.AccessDeniedPath = "/status/403";
        // Das Cookie endet mit dem JWT (Ablauf wird beim Anmelden gesetzt). Verlängern würde
        // nur ein Cookie mit abgelaufenem Token am Leben halten.
        options.SlidingExpiration = false;
    });
builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState();

builder.Services.AddHttpClient<IApiService, ApiService>((serviceProvider, client) =>
{
    var config = serviceProvider.GetRequiredService<IConfiguration>();
    client.BaseAddress = new Uri(config["ApiUrl"] ?? "http://localhost:5000");
    client.Timeout = TimeSpan.FromSeconds(30);
})
.ConfigurePrimaryHttpMessageHandler(serviceProvider =>
{
    var handler = new HttpClientHandler();

    // Nur nötig, solange der Server ein selbst signiertes Zertifikat verwendet.
    var config = serviceProvider.GetRequiredService<IConfiguration>();
    var validator = ServerCertificatePinning.CreateValidator(config["ServerCertificateThumbprint"]);
    if (validator != null)
    {
        handler.ServerCertificateCustomValidationCallback = validator;
    }

    return handler;
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    // Eigener Scope für die Fehlerseite: sonst rendert sie mit den Diensten der
    // fehlgeschlagenen Anfrage weiter, und Blazor bricht beim zweiten Rendern ab.
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

// Leere Fehlerantworten (v. a. 404 für unbekannte Adressen) durch eine Seite ersetzen.
app.UseStatusCodePagesWithReExecute("/status/{0}");

app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

// Abmelden nur per POST mit Antiforgery-Token: ein einfacher Link ließe sich von fremden
// Seiten auslösen und würde Benutzer ungefragt abmelden.
app.MapPost("/logout", async (HttpContext context, IAntiforgery antiforgery) =>
{
    if (!await antiforgery.IsRequestValidAsync(context))
        return Results.BadRequest();

    await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.LocalRedirect("~/login");
});

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
