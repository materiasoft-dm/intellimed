using System.Net.Http.Json;
using IntelliMed.Core.DTOs;

namespace IntelliMed.Web.Services;

public class UdfDefinitionService : IUdfDefinitionService
{
    private readonly HttpClient _httpClient;

    public UdfDefinitionService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<IEnumerable<UserDefinedFieldTypeDto>> GetAllActiveAsync()
    {
        return await _httpClient.GetFromJsonAsync<IEnumerable<UserDefinedFieldTypeDto>>("api/udf-definitions")
            ?? Enumerable.Empty<UserDefinedFieldTypeDto>();
    }
}
