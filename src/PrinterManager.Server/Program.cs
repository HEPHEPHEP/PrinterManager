using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using PrinterManager.Server.Data;
using PrinterManager.Server.Security;
using PrinterManager.Server.Services;
using PrinterManager.Server.Setup;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Pflichtgeheimnisse auflösen: konfigurierte Werte haben Vorrang, fehlende werden aus
// appsettings.Local.json ergänzt oder beim ersten Start erzeugt. Dadurch läuft eine
// frische Installation ohne vorbereitete Umgebungsvariablen.
var secrets = LocalSecrets.Ensure(builder.Configuration, builder.Environment.ContentRootPath);
if (secrets.Values.Count > 0)
{
    builder.Configuration.AddInMemoryCollection(secrets.Values);
}

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// Add Swagger with JWT support
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "PrinterManager API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Enter 'Bearer' [space] and then your token.",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// Add DbContext
builder.Services.AddDbContext<PrinterManagerDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

// Add Authentication — der Schlüssel ist an dieser Stelle garantiert vorhanden,
// weil LocalSecrets ihn sonst erzeugt hat.
var jwtKey = builder.Configuration["Jwt:Key"]!;
var clientAuthenticationMode = ClientAuthenticationOptions.GetMode(builder.Configuration);

var authentication = builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme);

if (clientAuthenticationMode == ClientAuthenticationMode.Windows)
{
    // Kerberos/NTLM für die Client-Endpunkte. Die Weboberfläche bleibt bei JWT.
    authentication.AddNegotiate();
}

authentication
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "PrinterManager",
            ValidAudience = builder.Configuration["Jwt:Audience"] ?? "PrinterManager",
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    });

// Standardmäßig ist JEDER Endpunkt geschützt. Endpunkte, die bewusst offen sein
// sollen (Login, Client-Registrierung), müssen [AllowAnonymous] tragen.
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

// Add services
builder.Services.AddScoped<IPrinterService, PrinterService>();
builder.Services.AddScoped<IAssignmentService, AssignmentService>();
builder.Services.AddScoped<IClientService, ClientService>();
builder.Services.AddScoped<IPrintServerScanService, PrintServerScanService>();
builder.Services.AddScoped<IAuthenticationService, AuthenticationService>();
builder.Services.AddScoped<ILdapService, LdapService>();
builder.Services.AddScoped<ISecurityConfigService, SecurityConfigService>();

// Add CORS — konfigurierbar über Cors:AllowedOrigins
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options =>
{
    options.AddPolicy("Configured", policy =>
    {
        if (allowedOrigins.Length > 0)
        {
            policy.WithOrigins(allowedOrigins)
                  .AllowAnyMethod()
                  .AllowAnyHeader();
        }
        else if (builder.Environment.IsDevelopment())
        {
            // Nur in Development: alle Origins erlauben
            policy.AllowAnyOrigin()
                  .AllowAnyMethod()
                  .AllowAnyHeader();
        }
        else
        {
            // Production ohne konfigurierte Origins: keine CORS-Requests erlaubt
            policy.WithOrigins("https://localhost")
                  .AllowAnyMethod()
                  .AllowAnyHeader();
        }
    });
});

// HTTPS-Endpunkt samt Zertifikat einrichten (übernimmt dabei den HTTP-Endpunkt).
var https = HttpsSetup.Configure(builder);

if (https.Enabled)
{
    // Ohne festen Port müsste die Umleitung ihn aus den Serveradressen erraten.
    builder.Services.AddHttpsRedirection(options => options.HttpsPort = https.Port);
}

// Standard-Port, solange nichts anderes konfiguriert ist. Eine feste Listen-Adresse
// würde "Urls", --urls und ASPNETCORE_URLS wirkungslos machen.
var urlsConfigured = !string.IsNullOrEmpty(builder.Configuration["Urls"])
    || builder.Configuration.GetSection("Kestrel:Endpoints").GetChildren().Any();

if (!urlsConfigured)
{
    builder.WebHost.ConfigureKestrel(options => options.ListenAnyIP(5000));
}

var app = builder.Build();

// Datenbank anlegen und beim ersten Start einen Administrator erzeugen.
await FirstRunSetup.RunAsync(app);

StartupReport.Write(app, secrets, https, clientAuthenticationMode);

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Umleitung erst aktivieren, wenn ein vertrauenswürdiges Zertifikat vorliegt — sonst
// laufen die Clients in Zertifikatsfehler statt in eine funktionierende Verbindung.
if (https.Enabled && app.Configuration.GetValue("Https:RedirectToHttps", false))
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseCors("Configured");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
