using PharmacyIS.Domain.Enums;

namespace PharmacyIS.Domain.Entities;

/// <summary>Документ „Продажба“ – заглавна част (касова бележка).</summary>
public class Sale
{
    public int SaleId { get; set; }

    /// <summary>Номер на документа, генериран автоматично от системата.</summary>
    public string DocNumber { get; set; } = string.Empty;

    /// <summary>Служител, извършил продажбата.</summary>
    public int UserId { get; set; }
    public string UserName { get; set; } = string.Empty;

    public DateTime SaleDate { get; set; } = DateTime.Now;

    /// <summary>Обща сума на продажбата в евро.</summary>
    public decimal TotalAmount { get; set; }

    /// <summary>Себестойност на продадените количества – основа за печалбата.</summary>
    public decimal TotalCost { get; set; }

    public PaymentType PaymentType { get; set; } = PaymentType.Cash;

    public DocumentStatus Status { get; set; } = DocumentStatus.Completed;

    public List<SaleItem> Items { get; set; } = new();

    /// <summary>Реализирана печалба от продажбата.</summary>
    public decimal Profit => Math.Round(TotalAmount - TotalCost, 2, MidpointRounding.AwayFromZero);
}

/// <summary>
/// Ред от документ „Продажба“. Един продаден артикул може да бъде изписан
/// от няколко партиди, затова редът съдържа и партидата, от която е изписан.
/// </summary>
public class SaleItem
{
    public int SaleItemId { get; set; }
    public int SaleId { get; set; }

    public int MedicineId { get; set; }
    public string MedicineCode { get; set; } = string.Empty;
    public string MedicineName { get; set; } = string.Empty;

    /// <summary>Партида, от която е изписано количеството.</summary>
    public int BatchId { get; set; }
    public string BatchNumber { get; set; } = string.Empty;

    public int Quantity { get; set; }

    /// <summary>Продажна цена за единица към момента на продажбата.</summary>
    public decimal UnitPrice { get; set; }

    /// <summary>Доставна цена за единица от съответната партида.</summary>
    public decimal UnitCost { get; set; }

    public decimal LineTotal => Math.Round(Quantity * UnitPrice, 2, MidpointRounding.AwayFromZero);
}
