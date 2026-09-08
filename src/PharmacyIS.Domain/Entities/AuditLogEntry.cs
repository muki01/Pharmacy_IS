namespace PharmacyIS.Domain.Entities;

/// <summary>
/// Запис в одитния дневник. Регистрират се опитите за вход в системата
/// и действията с повишен риск, което позволява последващ контрол.
/// </summary>
public class AuditLogEntry
{
    public int LogId { get; set; }
    public DateTime EventDate { get; set; } = DateTime.Now;

    /// <summary>Потребителско име, с което е извършено действието.</summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>Кратко наименование на събитието, например „LOGIN_FAILED“.</summary>
    public string Action { get; set; } = string.Empty;

    public string? Details { get; set; }

    public bool IsSuccess { get; set; }
}
