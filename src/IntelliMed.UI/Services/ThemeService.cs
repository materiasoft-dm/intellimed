using System.Threading.Tasks;
using Microsoft.JSInterop;

namespace IntelliMed.UI.Services
{
    public interface IThemeService
    {
        string CurrentTheme { get; }
        Task InitializeAsync();
        Task ToggleThemeAsync();
    }

    public class ThemeService : IThemeService
    {
        private readonly IClientStorage _storage;
        private readonly IJSRuntime _js;
        public string CurrentTheme { get; private set; } = "light";

        public ThemeService(IClientStorage storage, IJSRuntime js)
        {
            _storage = storage;
            _js = js;
        }

        public async Task InitializeAsync()
        {
            var t = await _storage.GetItemAsync("theme");
            if (!string.IsNullOrEmpty(t)) CurrentTheme = t!;
            await _js.InvokeVoidAsync("BlazorTheme.setTheme", CurrentTheme);
        }

        public async Task ToggleThemeAsync()
        {
            CurrentTheme = CurrentTheme == "dark" ? "light" : "dark";
            await _storage.SetItemAsync("theme", CurrentTheme);
            await _js.InvokeVoidAsync("BlazorTheme.setTheme", CurrentTheme);
        }
    }
}
