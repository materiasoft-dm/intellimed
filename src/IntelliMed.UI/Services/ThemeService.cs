using System.Threading.Tasks;
using Microsoft.JSInterop;

namespace IntelliMed.UI.Services
{
    public class ThemeService : IThemeService
    {
        private const string Key = "theme";
        private readonly IClientStorage _storage;
        private readonly IJSRuntime _js;
        private string _current = "light";

        public ThemeService(IClientStorage storage, IJSRuntime js)
        {
            _storage = storage;
            _js = js;
        }

        public async Task InitializeAsync()
        {
            var theme = await _storage.GetItemAsync(Key) ?? "light";
            _current = theme;
            await _js.InvokeVoidAsync("BlazorTheme.setTheme", theme);
        }

        public async Task ToggleThemeAsync()
        {
            var theme = _current == "dark" ? "light" : "dark";
            await SetThemeAsync(theme);
        }

        public async Task SetThemeAsync(string theme)
        {
            _current = theme;
            await _storage.SetItemAsync(Key, theme);
            await _js.InvokeVoidAsync("BlazorTheme.setTheme", theme);
        }

        public Task<string> GetThemeAsync() => Task.FromResult(_current);
    }
}
