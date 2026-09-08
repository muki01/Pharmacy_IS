namespace PharmacyIS.Domain.Entities;

/// <summary>
/// Партида от лекарствен продукт. Всяка доставка създава (или допълва)
/// партида с конкретен номер, срок на годност и доставна цена.
/// Наличността на аптеката е сумата от количествата по всички партиди.
/// </summary>
public class Batch
{
    public int BatchId { get; set; }

    public int MedicineId { get; set; }
    public string MedicineName { get; set; } = string.Empty;
    public string MedicineCode { get; set; } = string.Empty;

    /// <summary>Партиден номер, изписан от производителя върху опаковката.</summary>
    public string BatchNumber { get; set; } = string.Empty;

    /// <summary>Срок на годност на партидата.</summary>
    public DateTime ExpiryDate { get; set; }

    /// <summary>Текущо налично количество от партидата.</summary>
    public int Quantity { get; set; }

    /// <summary>Доставна цена за единица – основа за изчисляване на реализирана печалба.</summary>
    public decimal PurchasePrice { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    /// <summary>Партидата е с изтекъл срок към подадената дата.</summary>
    public bool IsExpired(DateTime asOf) => ExpiryDate.Date < asOf.Date;

    /// <summary>Оставащи дни до изтичане на срока на годност.</summary>
    public int DaysToExpiry(DateTime asOf) => (ExpiryDate.Date - asOf.Date).Days;
}
