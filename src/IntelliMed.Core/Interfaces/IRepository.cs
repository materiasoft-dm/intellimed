namespace IntelliMed.Core.Interfaces;

public interface IRepository<T> where T : class
{
    Task<T?> GetByIdAsync(int id);
    Task<IEnumerable<T>> GetAllAsync();
    Task<int> CreateAsync(T entity);
    Task UpdateAsync(int id, T entity);
    Task DeleteAsync(int id);
    Task<bool> ExistsAsync(int id);
}

public interface IPagedRepository<T> where T : class
{
    Task<(IEnumerable<T> Items, int TotalCount)> GetPagedAsync(
        int page = 1, 
        int pageSize = 20, 
        string? orderBy = null, 
        bool ascending = true);
}