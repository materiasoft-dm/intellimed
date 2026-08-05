using IntelliMed.Core.DTOs;
using IntelliMed.Core.Entities;
using IntelliMed.Core.Interfaces;
using IntelliMed.Infrastructure.Data;
using IntelliMed.Infrastructure.Mappers;
using Microsoft.EntityFrameworkCore;

namespace IntelliMed.Infrastructure.Repositories;

public class MedicineRepository : Repository<Medicine>, IMedicineRepository
{
    public MedicineRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<MedicineDto?> GetByIdAsync(int id)
    {
        var item = await _dbSet
            .Include(m => m.Ingredients).ThenInclude(mi => mi.ActiveIngredient)
            .FirstOrDefaultAsync(m => m.Id == id);
        return item == null ? null : EntityMapper.ToDto(item);
    }

    public async Task<IEnumerable<MedicineDto>> SearchAsync(string query, MedicineSource? source = null, int take = 20)
    {
        var baseQuery = _dbSet
            .Include(m => m.Ingredients).ThenInclude(mi => mi.ActiveIngredient)
            .Where(m => m.IsActive);

        if (source.HasValue)
        {
            baseQuery = baseQuery.Where(m => m.Source == source.Value);
        }

        if (string.IsNullOrWhiteSpace(query))
        {
            var recent = await baseQuery.OrderBy(m => m.Name).Take(take).ToListAsync();
            return recent.Select(EntityMapper.ToDto);
        }

        var searchTerm = query.ToLower();
        var items = await baseQuery
            .Where(m =>
                m.Name.ToLower().StartsWith(searchTerm) ||
                (m.GenericName != null && m.GenericName.ToLower().Contains(searchTerm)))
            .OrderBy(m => m.Name)
            .Take(take)
            .ToListAsync();
        return items.Select(EntityMapper.ToDto);
    }

    public async Task<int> CreateAsync(CreateMedicineDto dto)
    {
        var medicine = new Medicine
        {
            Name = dto.Name,
            GenericName = dto.GenericName,
            Strength = dto.Strength,
            Form = dto.Form,
            Manufacturer = dto.Manufacturer,
            IsPbsListed = dto.IsPbsListed,
            Schedule = dto.Schedule,
            Source = MedicineSource.Manual,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        await LinkIngredientsAsync(medicine, dto.ActiveIngredientNames);

        _dbSet.Add(medicine);
        await _context.SaveChangesAsync();
        return medicine.Id;
    }

    public async Task UpdateAsync(int id, UpdateMedicineDto dto)
    {
        var medicine = await _dbSet
            .Include(m => m.Ingredients)
            .FirstOrDefaultAsync(m => m.Id == id);
        if (medicine == null) return;

        medicine.Name = dto.Name;
        medicine.GenericName = dto.GenericName;
        medicine.Strength = dto.Strength;
        medicine.Form = dto.Form;
        medicine.Manufacturer = dto.Manufacturer;
        medicine.IsPbsListed = dto.IsPbsListed;
        medicine.Schedule = dto.Schedule;
        medicine.IsActive = dto.IsActive;

        // Relink from scratch against the submitted list rather than diffing — simpler, and this is a
        // low-cardinality list (a handful of ingredients per medicine at most).
        medicine.Ingredients.Clear();
        await LinkIngredientsAsync(medicine, dto.ActiveIngredientNames);

        await _context.SaveChangesAsync();
    }

    public async Task DeactivateAsync(int id)
    {
        var medicine = await _dbSet.FindAsync(id);
        if (medicine == null) return;

        medicine.IsActive = false;
        await _context.SaveChangesAsync();
    }

    private async Task LinkIngredientsAsync(Medicine medicine, string? namesText)
    {
        var names = (namesText ?? string.Empty)
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (names.Count == 0) return;

        var ingredientsByName = (await _context.ActiveIngredients.ToListAsync())
            .ToDictionary(i => i.Name, StringComparer.OrdinalIgnoreCase);

        foreach (var name in names)
        {
            if (!ingredientsByName.TryGetValue(name, out var ingredient))
            {
                ingredient = new ActiveIngredient { Name = name, IsAmtConfirmed = false };
                _context.ActiveIngredients.Add(ingredient);
                ingredientsByName[name] = ingredient;
            }
            medicine.Ingredients.Add(new MedicineIngredient { Medicine = medicine, ActiveIngredient = ingredient });
        }
    }
}
