using Microsoft.Maui.Controls;
using Microsoft.AspNetCore.Components.WebView.Maui;
using IntelliMed.UI;

namespace IntelliMed.Desktop;

public class MainPage : ContentPage
{
    public MainPage()
    {
        var blazorWebView = new BlazorWebView
        {
            HostPage = "wwwroot/index.html",
            VerticalOptions = LayoutOptions.FillAndExpand,
            HorizontalOptions = LayoutOptions.FillAndExpand
        };

        blazorWebView.RootComponents.Add(new RootComponent { Selector = "#app", ComponentType = typeof(IntelliMed.UI.AppRoot) });

        Content = blazorWebView;
    }
}
