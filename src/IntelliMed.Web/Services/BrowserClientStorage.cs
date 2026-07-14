using System.Threading.Tasks;
using Microsoft.JSInterop;
using IntelliMed.UI.Services;

namespace IntelliMed.Web.Services
{
    public class BrowserClientStorage : IClientStorage
    {
        private readonly IJSRuntime _js;

        public BrowserClientStorage(IJSRuntime js)
        {
            _js = js;
        }

        public async Task<string?> GetItemAsync(string key)
        {
            return await _js.InvokeAsync<string?>("localStorage.getItem", key);
        }

        public async Task SetItemAsync(string key, string? value)
        {
            await _js.InvokeVoidAsync("localStorage.setItem", key, value);
        }
    }
}
