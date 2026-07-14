using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using IntelliMed.Web;
using IntelliMed.UI.Services;
using IntelliMed.Web.Services;
using MudBlazor.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Configure HttpClient to point to the API
// In development, the API runs on http://localhost:5284
// In production, this should be configured via environment variables or settings
var apiBaseAddress = builder.HostEnvironment.BaseAddress.Contains("localhost")
    ? "http://localhost:5284/"
    : builder.HostEnvironment.BaseAddress;

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(apiBaseAddress) });

// Platform storage and theme service
builder.Services.AddScoped<IClientStorage, BrowserClientStorage>();
builder.Services.AddScoped<IThemeService, ThemeService>();
// Authentication service
builder.Services.AddScoped<IAuthService, AuthService>();
// Admin service for user/role management
builder.Services.AddScoped<IAdminService, AdminService>();
// MudBlazor services
builder.Services.AddMudServices();

var host = builder.Build();

// Initialize theme before running to avoid flash of unthemed UI
var themeService = host.Services.GetRequiredService<IThemeService>();
await themeService.InitializeAsync();

await host.RunAsync();
