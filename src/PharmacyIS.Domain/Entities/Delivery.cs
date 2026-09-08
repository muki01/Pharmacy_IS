using PharmacyIS.Domain.Enums;

namespace PharmacyIS.Domain.Entities;

/// <summary>Документ „Доставка“ – заглавна част.</summary>
public class Delivery
{
    public int DeliveryId { get; set; }

    /// <summary>Номер на документа, генериран автоматично от системата.</summary>
    public string DocNumber { get; set; } = string.Empty;

    public int SupplierId { get; set; }
    public string SupplierName { get; set; } = string.Empty;

    /// <summary>Служител, приел доставката.</summary>
    public int UserId { get; set; }
    public string UserName { get; set; } = string.Empty;

    public DateTime DeliveryDate { get; set; } = DateTime.Now;

    /// <summary>Номер на фактурата на доставчика.</summary>
    public string? InvoiceNumber { get; set; }

    public decimal TotalAmount { get; set; }

    public DocumentStatus Status { get; set; } = DocumentStatus.Completed;

    public string? Note { get; set; }

    public List<DeliveryItem> Items { get; set; } = new();
}

/// <summary>Ред от документ „Доставка“ – доставено количество от една партида.</summary>
public class DeliveryItem
{
    public int DeliveryItemId { get; set; }
    public int DeliveryId { get; set; }

    public int MedicineId { get; set; }
    public string MedicineCode { get; set; } = string.Empty;
    public string MedicineName { get; set; } = string.Empty;

    public string BatchNumber { get; set; } = string.Empty;
    public DateTime ExpiryDate { get; set; }

    public int Quantity { get; set; }

    /// <summary>Доставна цена за единица.</summary>
    public decimal UnitPrice { get; set; }

    /// <summary>Стойност на реда – изчислява се, а не се въвежда ръчно.</summary>
    public decimal LineTotal => Math.Round(Quantity * UnitPrice, 2, MidpointRounding.AwayFromZero);
}
