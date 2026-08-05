using IntelliMed.Core.DTOs;

namespace IntelliMed.Core.Interfaces;

public interface IIngredientEnrichmentService
{
    Task<IngredientEnrichmentResultDto> EnrichFromWikidataAsync();
}
