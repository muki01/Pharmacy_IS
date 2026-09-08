namespace PharmacyIS.Domain.Entities;

/// <summary>Номенклатура „Лекарствена форма“ – таблетки, сироп, капсули и др.</summary>
public class DosageForm
{
    public int FormId { get; set; }

    /// <summary>Двуразряден номенклатурен код.</summary>
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public override string ToString() => Name;
}
