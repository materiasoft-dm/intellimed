using System.Net.Http.Json;
using IntelliMed.Core.DTOs;

namespace IntelliMed.Web.Services;

public class ProviderGroupService : IProviderGroupService
{
    private readonly HttpClient _httpClient;

    public ProviderGroupService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<IEnumerable<ProviderGroupDto>> GetAllActiveAsync()
    {
        return await _httpClient.GetFromJsonAsync<IEnumerable<ProviderGroupDto>>("api/provider-groups")
            ?? Enumerable.Empty<ProviderGroupDto>();
    }
}
