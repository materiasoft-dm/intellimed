using IntelliMed.Core.DTOs;

namespace IntelliMed.Web.Services;

public interface IPractitionerService
{
    Task<List<PractitionerDto>> GetAllActiveAsync();
}
