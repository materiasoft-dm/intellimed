using System.Net.Http.Json;
using IntelliMed.Core.DTOs;

namespace IntelliMed.Web.Services;

public class AddressBookService : IAddressBookService
{
    private readonly HttpClient _httpClient;

    public AddressBookService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<PagedResult<AddressBookContactDto>> SearchAsync(AddressBookSearchDto search)
    {
        var args = new List<string> { $"page={search.Page}", $"pageSize={search.PageSize}" };
        if (!string.IsNullOrWhiteSpace(search.Query)) args.Add($"query={Uri.EscapeDataString(search.Query)}");
        if (search.IncludeArchived) args.Add("includeArchived=true");

        var result = await _httpClient.GetFromJsonAsync<PagedResult<AddressBookContactDto>>(
            "api/address-book?" + string.Join("&", args));
        return result ?? new PagedResult<AddressBookContactDto>();
    }

    public async Task<AddressBookContactDto?> GetByIdAsync(int id)
    {
        var response = await _httpClient.GetAsync($"api/address-book/{id}");
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<AddressBookContactDto>();
    }

    private record CreateResult(int Id);

    public async Task<int?> CreateAsync(SaveAddressBookContactDto dto)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/address-book", dto);
            if (!response.IsSuccessStatusCode) return null;
            var result = await response.Content.ReadFromJsonAsync<CreateResult>();
            return result?.Id;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Create address book contact error: {ex.Message}");
            return null;
        }
    }

    public async Task<bool> UpdateAsync(int id, SaveAddressBookContactDto dto)
    {
        try
        {
            var response = await _httpClient.PutAsJsonAsync($"api/address-book/{id}", dto);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Update address book contact error: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> ArchiveAsync(int id)
    {
        try
        {
            var response = await _httpClient.PostAsync($"api/address-book/{id}/archive", null);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Archive address book contact error: {ex.Message}");
            return false;
        }
    }
}
