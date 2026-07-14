using System.Threading.Tasks;

namespace IntelliMed.UI.Services
{
    public interface IClientStorage
    {
        Task<string?> GetItemAsync(string key);
        Task SetItemAsync(string key, string? value);
    }
}
