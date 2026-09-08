using PharmacyIS.Domain.Entities;
using PharmacyIS.Domain.Reports;

namespace PharmacyIS.Services.Models;

/// <summary>
/// Обобщение на предупрежденията за състоянието на склада.
/// Показва се при стартиране на приложението и при вход в модул „Склад“.
/// </summary>
public class StockAlerts
{
    /// <summary>Момент, към който са изчислени предупрежденията.</summary>
    public DateTime AsOf { get; set; } = DateTime.Now;

    /// <summary>Използван праг за предупреждение при изтичащ срок, в дни.</summary>
    public int ExpiryWarningDays { get; set; }

    /// <summary>Продукти с наличност под или равна на минималния запас.</summary>
    public List<StockRow> LowStock { get; set; } = new();

    /// <summary>Партиди с вече изтекъл срок на годност, които са все още в наличност.</summary>
    public List<Batch> Expired { get; set; } = new();

    /// <summary>Партиди, чийто срок изтича в рамките на прага за предупреждение.</summary>
    public List<Batch> ExpiringSoon { get; set; } = new();

    /// <summary>Общ брой предупреждения.</summary>
    public int TotalCount => LowStock.Count + Expired.Count + ExpiringSoon.Count;

    /// <summary>Има ли изобщо какво да се съобщи на потребителя.</summary>
    public bool HasAlerts => TotalCount > 0;

    /// <summary>Кратко описание на предупрежденията за лентата на състоянието.</summary>
    public string Summary => HasAlerts
        ? $"Ниска наличност: {LowStock.Count} · Изтичащ срок: {ExpiringSoon.Count} · Изтекъл срок: {Expired.Count}"
        : "Няма активни предупреждения.";
}
