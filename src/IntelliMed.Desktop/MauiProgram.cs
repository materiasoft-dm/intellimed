using Microsoft.Maui;
using Microsoft.Maui.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Components.WebView.Maui;
using MudBlazor.Services;

namespace IntelliMed.Desktop;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts => { });

        // Register BlazorWebView and services on the builder.Services
        builder.Services.AddMauiBlazorWebView();
#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
#endif

        // register platform storage and theme service for the BlazorWebView host
        builder.Services.AddSingleton<IntelliMed.UI.Services.IClientStorage, IntelliMed.Desktop.Services.MAUIClientStorage>();
        builder.Services.AddScoped<IntelliMed.UI.Services.IThemeService, IntelliMed.UI.Services.ThemeService>();
        builder.Services.AddMudServices();

        return builder.Build();
    }
}
