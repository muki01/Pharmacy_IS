using PharmacyIS.Domain.Enums;

namespace PharmacyIS.Services.Models;

/// <summary>
/// Заявка за регистриране на продажба, така както я подава касовият екран.
/// Съдържа само това, което операторът въвежда – цените и партидите
/// се определят от бизнес логиката.
/// </summary>
public class SaleRequest
{
    /// <summary>Служител, извършващ продажбата.</summary>
    public int UserId { get; set; }

    /// <summary>Начин на плащане.</summary>
    public PaymentType PaymentType { get; set; } = PaymentType.Cash;

    /// <summary>
    /// Дата и час на продажбата. Използва се основно при зареждане
    /// на демонстрационни данни; при работа на каса се подава текущият момент.
    /// </summary>
    public DateTime SaleDate { get; set; } = DateTime.Now;

    /// <summary>Продавани позиции.</summary>
    public List<SaleRequestLine> Lines { get; set; } = new();
}

/// <summary>Една позиция от заявката за продажба.</summary>
public class SaleRequestLine
{
    public int MedicineId { get; set; }
    public int Quantity { get; set; }

    public SaleRequestLine() { }

    public SaleRequestLine(int medicineId, int quantity)
    {
        MedicineId = medicineId;
        Quantity = quantity;
    }
}
