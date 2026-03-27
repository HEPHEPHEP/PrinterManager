using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using PrinterManager.Server.Data;
using PrinterManager.Server.Services;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

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

// Add Authentication
var jwtKey = builder.Configuration["Jwt:Key"];
if (string.IsNullOrEmpty(jwtKey) || jwtKey.Length < 32)
{
    throw new InvalidOperationException(
        "FEHLER: Jwt:Key ist nicht konfiguriert oder zu kurz (min. 32 Zeichen). " +
        "Setze den Wert in appsettings.json, appsettings.Production.json oder als Umgebungsvariable Jwt__Key.");
}
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
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

builder.Services.AddAuthorization();

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

// Configure HTTP only by default (HTTPS can be enabled in UI)
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(5000); // HTTP
});

var app = builder.Build();

// Ensure database is created
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<PrinterManagerDbContext>();
    db.Database.EnsureCreated();
    
    // Admin-Benutzer beim ersten Start erstellen (mit BCrypt-Hash)
    if (!db.ApplicationUsers.Any(u => u.Role == PrinterManager.Shared.Models.UserRole.Administrator))
    {
        var adminPassword = builder.Configuration["AdminPassword"]
            ?? Environment.GetEnvironmentVariable("ADMIN_PASSWORD");
        
        if (string.IsNullOrEmpty(adminPassword) || adminPassword.Length < 8)
        {
            throw new InvalidOperationException(
                "FEHLER: Kein Admin-Benutzer vorhanden und ADMIN_PASSWORD nicht gesetzt (min. 8 Zeichen). " +
                "Setze die Umgebungsvariable ADMIN_PASSWORD beim ersten Start.");
        }
        
        db.ApplicationUsers.Add(new PrinterManager.Shared.Models.ApplicationUser
        {
            Username = "admin",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(adminPassword, workFactor: 12),
            IsActive = true,
            IsLdapUser = false,
            Role = PrinterManager.Shared.Models.UserRole.Administrator,
            CreatedAt = DateTime.UtcNow
        });
        db.SaveChanges();
        
        Console.WriteLine("✓ Admin-Benutzer erstellt: admin (Passwort aus ADMIN_PASSWORD)");
    }
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Don't force HTTPS redirection (can be enabled via UI)
app.UseCors("Configured");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
