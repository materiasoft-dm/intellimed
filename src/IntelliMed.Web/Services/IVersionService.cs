namespace IntelliMed.Web.Services;

public interface IVersionService
{
    Task<string> GetVersionAsync();
}
