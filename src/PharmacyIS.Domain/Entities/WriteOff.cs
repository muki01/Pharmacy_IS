using PharmacyIS.Domain.Enums;

namespace PharmacyIS.Domain.Entities;

/// <summary>
/// Протокол за брак. Използва се при изтекъл срок на годност
/// или при повредена опаковка – количествата се изписват от наличността.
/// </summary>
public class WriteOff
{
    public int WriteOffId { get; set; }
    public string DocNumber { get; set; } = string.Empty;

    public int UserId { get; set; }
    public string UserName { get; set; } = string.Empty;

    public DateTime WriteOffDate { get; set; } = DateTime.Now;

    /// <summary>Основание за брак.</summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>Отчетна стойност на бракуваните количества по доставна цена.</summary>
    public decimal TotalCost { get; set; }

    public DocumentStatus Status { get; set; } = DocumentStatus.Completed;

    public List<WriteOffItem> Items { get; set; } = new();
}

/// <summary>Ред от протокол за брак.</summary>
public class WriteOffItem
{
    public int WriteOffItemId { get; set; }
    public int WriteOffId { get; set; }

    public int MedicineId { get; set; }
    public string MedicineCode { get; set; } = string.Empty;
    public string MedicineName { get; set; } = string.Empty;

    public int BatchId { get; set; }
    public string BatchNumber { get; set; } = string.Empty;
    public DateTime ExpiryDate { get; set; }

    public int Quantity { get; set; }

    /// <summary>Доставна цена за единица.</summary>
    public decimal UnitCost { get; set; }

    public decimal LineTotal => Math.Round(Quantity * UnitCost, 2, MidpointRounding.AwayFromZero);
}
