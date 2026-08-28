using IntelliMed.Core.DTOs;
using IntelliMed.Core.Entities;
using IntelliMed.Core.Interfaces;
using IntelliMed.Infrastructure.Data;
using IntelliMed.Infrastructure.Mappers;
using Microsoft.EntityFrameworkCore;

namespace IntelliMed.Infrastructure.Repositories;

public class DrugInteractionRepository : Repository<DrugInteraction>, IDrugInteractionRepository
{
    public DrugInteractionRepository(AppDbContext context) : base(context)
    {
    }

    public new async Task<DrugInteractionDto?> GetByIdAsync(int id)
    {
        var row = await _dbSet.Include(d => d.IngredientA).Include(d => d.IngredientB)
            .FirstOrDefaultAsync(d => d.Id == id);
        return row == null ? null : EntityMapper.ToDto(row);
    }

    public async Task<IEnumerable<DrugInteractionDto>> GetAllAsync(int take = 200)
    {
        var rows = await _dbSet
            .Include(d => d.IngredientA)
            .Include(d => d.IngredientB)
            .Where(d => d.IsActive)
            .OrderBy(d => d.IngredientA!.Name).ThenBy(d => d.IngredientB!.Name)
            .Take(take)
            .ToListAsync();
        return rows.Select(EntityMapper.ToDto);
    }

    public async Task<int> CreateAsync(CreateDrugInteractionDto dto)
    {
        var ingredientA = await FindOrCreateIngredientAsync(dto.IngredientAName);
        var ingredientB = await FindOrCreateIngredientAsync(dto.IngredientBName);
        var (idA, idB) = CanonicalOrder(ingredientA.Id, ingredientB.Id);

        var interaction = new DrugInteraction
        {
            IngredientAId = idA,
            IngredientBId = idB,
            Severity = dto.Severity,
            Description = dto.Description,
            SourceDataset = dto.SourceDataset,
            IsActive = true
        };
        await _dbSet.AddAsync(interaction);
        await _context.SaveChangesAsync();
        return interaction.Id;
    }

    public async Task UpdateAsync(int id, UpdateDrugInteractionDto dto)
    {
        var interaction = await _dbSet.FindAsync(id);
        if (interaction == null)
            throw new InvalidOperationException($"DrugInteraction with ID {id} not found");

        interaction.Severity = dto.Severity;
        interaction.Description = dto.Description;
        interaction.SourceDataset = dto.SourceDataset;
        interaction.IsActive = dto.IsActive;
        await _context.SaveChangesAsync();
    }

    public Task<int> CountAsync() => _dbSet.CountAsync();

    public async Task<List<DrugInteraction>> FindInteractionsAsync(
        IReadOnlyCollection<int> candidateIngredientIds, IReadOnlyCollection<int> existingIngredientIds)
    {
        if (candidateIngredientIds.Count == 0 || existingIngredientIds.Count == 0)
            return new List<DrugInteraction>();

        return await _dbSet
            .Include(d => d.IngredientA)
            .Include(d => d.IngredientB)
            .Where(d => d.IsActive && (
                (candidateIngredientIds.Contains(d.IngredientAId) && existingIngredientIds.Contains(d.IngredientBId)) ||
                (candidateIngredientIds.Contains(d.IngredientBId) && existingIngredientIds.Contains(d.IngredientAId))
            ))
            .ToListAsync();
    }

    // Canonical A<B ordering so the unique compound index actually enforces "no duplicate pair"
    // regardless of which order a CSV/admin form lists the two ingredients in.
    private static (int A, int B) CanonicalOrder(int idA, int idB) => idA < idB ? (idA, idB) : (idB, idA);

    private async Task<ActiveIngredient> FindOrCreateIngredientAsync(string name)
    {
        var trimmed = name.Trim();
        var existing = await _context.ActiveIngredients
            .FirstOrDefaultAsync(i => i.Name.ToLower() == trimmed.ToLower());
        if (existing != null) return existing;

        var created = new ActiveIngredient { Name = trimmed, IsAmtConfirmed = false };
        _context.ActiveIngredients.Add(created);
        await _context.SaveChangesAsync();
        return created;
    }
}
