namespace PharmacyIS.Domain.Entities;

/// <summary>
/// Доставчик на лекарствени продукти.
/// Кодът се формира по номенклатурата, описана в документацията.
/// </summary>
public class Supplier
{
    public int SupplierId { get; set; }

    /// <summary>Номенклатурен код на доставчика (четириразряден).</summary>
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    /// <summary>ЕИК / БУЛСТАТ на търговското дружество.</summary>
    public string? Bulstat { get; set; }

    public string? ContactPerson { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }

    public bool IsActive { get; set; } = true;

    public override string ToString() => $"{Code} – {Name}";
}
