namespace IntelliMed.Core.Entities;

/// <summary>Laterality of a health issue — Medinet's SIDE. Nullable on the entity, since most
/// conditions aren't sided at all. Persisted as a plain int, so append only, never renumber.</summary>
public enum BodySide
{
    Left,
    Right,
    Bilateral
}
