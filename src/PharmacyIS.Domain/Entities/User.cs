using PharmacyIS.Domain.Enums;

namespace PharmacyIS.Domain.Entities;

/// <summary>
/// Потребител на системата (служител на аптеката).
/// Паролата никога не се съхранява в явен вид – пази се само
/// PBKDF2 отпечатък заедно с индивидуална сол.
/// </summary>
public class User
{
    public int UserId { get; set; }

    /// <summary>Потребителско име за вход – уникално в рамките на системата.</summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>Base64 представяне на PBKDF2 отпечатъка на паролата.</summary>
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>Base64 представяне на индивидуалната сол на потребителя.</summary>
    public string PasswordSalt { get; set; } = string.Empty;

    /// <summary>Име и фамилия на служителя.</summary>
    public string FullName { get; set; } = string.Empty;

    public UserRole Role { get; set; } = UserRole.Pharmacist;

    /// <summary>Активните потребители могат да влизат в системата.</summary>
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    /// <summary>Наименование на ролята за визуализация в интерфейса.</summary>
    public string RoleName => Role == UserRole.Manager ? "Управител" : "Фармацевт";
}
