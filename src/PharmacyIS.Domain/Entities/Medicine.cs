namespace PharmacyIS.Domain.Entities;

/// <summary>
/// Лекарствен продукт от каталога на аптеката.
/// Наличното количество и срокът на годност не се пазят в този запис,
/// а в свързаните партиди (<see cref="Batch"/>), защото един и същ продукт
/// постъпва на партиди с различни срокове на годност и различни доставни цени.
/// </summary>
public class Medicine
{
    public int MedicineId { get; set; }

    /// <summary>Номенклатурен код на продукта (осемразряден, разряден код).</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Търговско наименование на продукта.</summary>
    public string Name { get; set; } = string.Empty;

    public int IngredientId { get; set; }
    public string IngredientName { get; set; } = string.Empty;

    public int FormId { get; set; }
    public string FormName { get; set; } = string.Empty;

    /// <summary>Производител на лекарствения продукт.</summary>
    public string? Manufacturer { get; set; }

    /// <summary>Дозировка, например „500 mg“.</summary>
    public string? Strength { get; set; }

    /// <summary>Продажна цена на дребно с включено ДДС, в евро.</summary>
    public decimal Price { get; set; }

    /// <summary>Признак „отпуска се само по лекарско предписание“.</summary>
    public bool RequiresPrescription { get; set; }

    /// <summary>Минимална складова наличност, под която системата издава предупреждение.</summary>
    public int MinStock { get; set; } = 10;

    /// <summary>Баркод (EAN-13) за бързо намиране на касата.</summary>
    public string? Barcode { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Обща налична наличност – изчислява се като сума от количествата
    /// по всички неизчерпани партиди. Попълва се от слоя за достъп до данни.
    /// </summary>
    public int StockQuantity { get; set; }

    /// <summary>Най-близкият срок на годност сред наличните партиди.</summary>
    public DateTime? NearestExpiry { get; set; }

    public override string ToString() => $"{Code} – {Name}";
}
