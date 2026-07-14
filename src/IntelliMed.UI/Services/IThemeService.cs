using System.Threading.Tasks;

namespace IntelliMed.UI.Services
{
    public interface IThemeService
    {
        Task InitializeAsync();
        Task ToggleThemeAsync();
        Task SetThemeAsync(string theme);
        Task<string> GetThemeAsync();
    }
}
