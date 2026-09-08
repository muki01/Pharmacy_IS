namespace PharmacyIS.Services.Models;

/// <summary>Заявка за заприхождаване на доставка.</summary>
public class DeliveryRequest
{
    public int SupplierId { get; set; }
    public int UserId { get; set; }
    public DateTime DeliveryDate { get; set; } = DateTime.Now;
    public string? InvoiceNumber { get; set; }
    public string? Note { get; set; }

    public List<DeliveryRequestLine> Lines { get; set; } = new();
}

/// <summary>Една позиция от заявката за доставка.</summary>
public class DeliveryRequestLine
{
    public int MedicineId { get; set; }

    /// <summary>Партиден номер от опаковката.</summary>
    public string BatchNumber { get; set; } = string.Empty;

    public DateTime ExpiryDate { get; set; }

    public int Quantity { get; set; }

    /// <summary>Доставна цена за единица.</summary>
    public decimal UnitPrice { get; set; }
}
