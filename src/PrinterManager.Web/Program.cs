using PrinterManager.Web.Components;
using PrinterManager.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddHttpClient<IApiService, ApiService>((serviceProvider, client) =>
{
    var config = serviceProvider.GetRequiredService<IConfiguration>();
    var apiUrl = config["ApiUrl"] ?? "http://localhost:5000";
    client.BaseAddress = new Uri(apiUrl);
    Console.WriteLine($"Configuring HttpClient with BaseAddress: {apiUrl}");
});
builder.Services.AddSingleton<AuthStateService>();

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
