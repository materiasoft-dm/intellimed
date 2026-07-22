using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using IntelliMed.Web;
using IntelliMed.UI.Services;
using IntelliMed.Web.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Configure HttpClient — hosted model: API is on the same origin
// The Blazor WASM app is served by the API project, so BaseAddress is the same origin
// AuthHeaderHandler attaches the JWT token from storage to every request
builder.Services.AddScoped<AuthHeaderHandler>();
builder.Services.AddScoped(sp =>
{
    var handler = sp.GetRequiredService<AuthHeaderHandler>();
    handler.InnerHandler = new HttpClientHandler();
    return new HttpClient(handler) { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) };
});

// Platform storage and theme service
builder.Services.AddScoped<IClientStorage, BrowserClientStorage>();
builder.Services.AddScoped<IThemeService, ThemeService>();
// Authentication service
builder.Services.AddScoped<IAuthService, AuthService>();
// Admin service for user/role management
builder.Services.AddScoped<IAdminService, AdminService>();
// Front-end error logging service
builder.Services.AddScoped<ILoggerService, LoggerService>();
// Patient management service
builder.Services.AddScoped<IPatientService, PatientService>();

var host = builder.Build();

// Initialize theme before running to avoid flash of unthemed UI
var themeService = host.Services.GetRequiredService<IThemeService>();
await themeService.InitializeAsync();

await host.RunAsync();
