using System.Threading.Tasks;
using IntelliMed.UI.Services;
using Microsoft.Maui.Storage;

namespace IntelliMed.Desktop.Services
{
    public class MAUIClientStorage : IClientStorage
    {
        public Task<string?> GetItemAsync(string key)
        {
            var value = Preferences.Get(key, null as string);
            return Task.FromResult(value);
        }

        public Task SetItemAsync(string key, string? value)
        {
            if (value is null)
                Preferences.Remove(key);
            else
                Preferences.Set(key, value);

            return Task.CompletedTask;
        }
    }
}
