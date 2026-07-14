using Microsoft.Maui.Controls;

namespace IntelliMed.Desktop;

public partial class App : Application
{
    public App()
    {
        // Code-only App: set main page directly without XAML initialization
        MainPage = new MainPage();
    }
}
