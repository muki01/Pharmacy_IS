namespace PharmacyIS.Domain.Entities;

/// <summary>
/// Номенклатура „Активна съставка“ (INN – международно непатентно наименование).
/// Позволява търсене на всички търговски продукти с една и съща съставка.
/// </summary>
public class ActiveIngredient
{
    public int IngredientId { get; set; }

    /// <summary>Триразряден номенклатурен код.</summary>
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public override string ToString() => Name;
}
