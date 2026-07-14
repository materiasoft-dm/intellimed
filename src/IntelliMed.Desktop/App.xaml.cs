using Microsoft.Maui.Controls;

namespace IntelliMed.Desktop;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();
        MainPage = new MainPage();
    }
}
