using IntelliMed.Core.DTOs;
using IntelliMed.Core.Entities;
using IntelliMed.Core.Interfaces;
using IntelliMed.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace IntelliMed.Infrastructure.Repositories;

public class SearchActionRepository : Repository<SearchAction>, ISearchActionRepository
{
    public SearchActionRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<SearchActionDto?> GetByIdAsync(int id)
    {
        var action = await _dbSet.FirstOrDefaultAsync(a => a.Id == id);
        return action == null ? null : ToDto(action);
    }

    public async Task<IEnumerable<SearchActionDto>> GetAllActiveAsync()
    {
        var actions = await _dbSet
            .Where(a => a.IsActive)
            .OrderBy(a => a.SortOrder)
            .ThenBy(a => a.Title)
            .ToListAsync();
        return actions.Select(ToDto);
    }

    public async Task<int> CreateAsync(SaveSearchActionRequest request)
    {
        var action = new SearchAction
        {
            Title = request.Title,
            Keywords = request.Keywords,
            Description = request.Description,
            Category = request.Category,
            ActionType = request.ActionType,
            Target = request.Target,
            PageKey = request.PageKey,
            SortOrder = request.SortOrder
        };

        await _dbSet.AddAsync(action);
        await _context.SaveChangesAsync();
        return action.Id;
    }

    public async Task UpdateAsync(int id, SaveSearchActionRequest request)
    {
        var action = await _dbSet.FindAsync(id);
        if (action == null)
            throw new InvalidOperationException($"SearchAction with ID {id} not found");

        action.Title = request.Title;
        action.Keywords = request.Keywords;
        action.Description = request.Description;
        action.Category = request.Category;
        action.ActionType = request.ActionType;
        action.Target = request.Target;
        action.PageKey = request.PageKey;
        action.SortOrder = request.SortOrder;

        await _context.SaveChangesAsync();
    }

    public async Task ArchiveAsync(int id)
    {
        var action = await _dbSet.FindAsync(id);
        if (action == null)
            throw new InvalidOperationException($"SearchAction with ID {id} not found");

        action.IsActive = false;
        await _context.SaveChangesAsync();
    }

    private static SearchActionDto ToDto(SearchAction a) => new()
    {
        Id = a.Id,
        Title = a.Title,
        Keywords = a.Keywords,
        Description = a.Description,
        Category = a.Category,
        ActionType = a.ActionType,
        Target = a.Target,
        PageKey = a.PageKey,
        SortOrder = a.SortOrder,
        IsActive = a.IsActive
    };
}
