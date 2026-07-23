using System.Net.Http.Json;
using IntelliMed.Core.DTOs;

namespace IntelliMed.Web.Services;

public class PatientService : IPatientService
{
    private readonly HttpClient _httpClient;

    public PatientService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<PagedResult<PatientDto>> SearchPatientsAsync(PatientSearchDto search)
    {
        var args = new List<string>
        {
            $"page={search.Page}",
            $"pageSize={search.PageSize}",
            $"includeArchived={search.IncludeArchived}"
        };

        void AddIfSet(string key, string? value)
        {
            if (!string.IsNullOrWhiteSpace(value))
                args.Add($"{key}={Uri.EscapeDataString(value)}");
        }

        void AddDate(string key, DateTime? value)
        {
            if (value.HasValue)
                args.Add($"{key}={value.Value:yyyy-MM-dd}");
        }

        void AddBool(string key, bool? value)
        {
            if (value.HasValue)
                args.Add($"{key}={value.Value}");
        }

        AddIfSet("query", search.Query);
        if (search.IsActive.HasValue) args.Add($"isActive={search.IsActive.Value}");

        AddIfSet("surname", search.Surname);
        AddIfSet("givenName", search.GivenName);
        AddIfSet("medicareNumber", search.MedicareNumber);
        if (search.Gender.HasValue) args.Add($"gender={search.Gender.Value}");
        AddIfSet("dvaNumber", search.DvaNumber);
        AddIfSet("fileNumber", search.FileNumber);
        AddIfSet("pensionNumber", search.PensionNumber);
        AddIfSet("healthFundNumber", search.HealthFundNumber);
        AddIfSet("lifeCardNum", search.LifeCardNum);
        AddDate("dobFrom", search.DobFrom);
        AddDate("dobTo", search.DobTo);

        AddIfSet("address", search.Address);
        AddIfSet("suburb", search.Suburb);
        AddIfSet("postcode", search.Postcode);
        AddIfSet("state", search.State);

        AddIfSet("postalAddress", search.PostalAddress);
        AddIfSet("postalSuburb", search.PostalSuburb);
        AddIfSet("postalPostcode", search.PostalPostcode);
        AddIfSet("postalState", search.PostalState);

        AddIfSet("homePhone", search.HomePhone);
        AddIfSet("businessHoursPhone", search.BusinessHoursPhone);
        AddIfSet("mobilePhone", search.MobilePhone);
        AddIfSet("email", search.Email);
        if (search.AtsiStatus.HasValue) args.Add($"atsiStatus={search.AtsiStatus.Value}");

        AddDate("createdFrom", search.CreatedFrom);
        AddDate("createdTo", search.CreatedTo);
        AddDate("medicareExpiryFrom", search.MedicareExpiryFrom);
        AddDate("medicareExpiryTo", search.MedicareExpiryTo);
        AddDate("healthFundJoinFrom", search.HealthFundJoinFrom);
        AddDate("healthFundJoinTo", search.HealthFundJoinTo);

        AddIfSet("warnings", search.Warnings);
        AddIfSet("notes", search.Notes);
        AddIfSet("referredBy", search.ReferredBy);
        if (search.PatientType.HasValue) args.Add($"patientType={search.PatientType.Value}");
        AddIfSet("urNumber", search.UrNumber);
        if (search.HealthFundId.HasValue) args.Add($"healthFundId={search.HealthFundId.Value}");
        if (search.PayerPatientId.HasValue) args.Add($"payerPatientId={search.PayerPatientId.Value}");
        if (search.AccountTypes is { Count: > 0 })
        {
            foreach (var type in search.AccountTypes)
                args.Add($"accountTypes={type}");
        }

        if (search.Deceased.HasValue) args.Add($"deceased={search.Deceased.Value}");
        AddBool("acceptEmail", search.AcceptEmail);
        AddBool("acceptSms", search.AcceptSms);
        AddBool("acceptSmsMarketing", search.AcceptSmsMarketing);

        var uri = "api/patients/search?" + string.Join("&", args);
        return await _httpClient.GetFromJsonAsync<PagedResult<PatientDto>>(uri)
            ?? new PagedResult<PatientDto>();
    }

    private record CreateResult(int Id);

    public async Task<int?> CreatePatientAsync(CreatePatientDto dto)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/patients", dto);
            if (!response.IsSuccessStatusCode) return null;
            var result = await response.Content.ReadFromJsonAsync<CreateResult>();
            return result?.Id;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Create patient error: {ex.Message}");
            return null;
        }
    }

    public async Task<PatientDto?> GetPatientByIdAsync(int id)
    {
        var response = await _httpClient.GetAsync($"api/patients/{id}");
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<PatientDto>();
    }

    public async Task<bool> UpdatePatientAsync(int id, UpdatePatientDto dto)
    {
        try
        {
            var response = await _httpClient.PutAsJsonAsync($"api/patients/{id}", dto);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Update patient error: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> ArchivePatientAsync(int id)
    {
        try
        {
            var response = await _httpClient.DeleteAsync($"api/patients/{id}");
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Archive patient error: {ex.Message}");
            return false;
        }
    }
}
