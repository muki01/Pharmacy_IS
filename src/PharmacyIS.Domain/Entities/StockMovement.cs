using PharmacyIS.Domain.Enums;

namespace PharmacyIS.Domain.Entities;

/// <summary>
/// Запис в журнала на складовите движения. Всяко изменение на наличността –
/// доставка, продажба, брак или сторно – създава ред в този журнал.
/// Журналът позволява контролно пресмятане: сумата от движенията по дадена
/// партида винаги трябва да е равна на текущото ѝ количество.
/// </summary>
public class StockMovement
{
    public int MovementId { get; set; }

    public int MedicineId { get; set; }
    public string MedicineName { get; set; } = string.Empty;

    public int BatchId { get; set; }
    public string BatchNumber { get; set; } = string.Empty;

    public MovementType MovementType { get; set; }

    /// <summary>
    /// Количество със знак: положително при приход и сторно,
    /// отрицателно при разход и брак.
    /// </summary>
    public int Quantity { get; set; }

    /// <summary>Вид на документа-основание: „Доставка“, „Продажба“, „Брак“.</summary>
    public string DocType { get; set; } = string.Empty;

    /// <summary>Номер на документа-основание.</summary>
    public string DocNumber { get; set; } = string.Empty;

    public DateTime MovementDate { get; set; } = DateTime.Now;

    public int UserId { get; set; }
    public string UserName { get; set; } = string.Empty;

    public string MovementTypeName => MovementType switch
    {
        MovementType.In => "Приход",
        MovementType.Out => "Разход",
        MovementType.WriteOff => "Брак",
        MovementType.Reversal => "Сторно",
        _ => "Неизвестно"
    };
}
