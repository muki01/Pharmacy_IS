namespace PharmacyIS.Domain.Enums;

/// <summary>
/// Роля на потребителя в системата. Ролята определя нивото на достъп
/// до отделните функционални модули (Role-Based Access Control).
/// </summary>
public enum UserRole
{
    /// <summary>Фармацевт – ежедневни операции: продажби, доставки, търсене, склад.</summary>
    Pharmacist = 1,

    /// <summary>Управител – всички права на фармацевта плюс справки, печалба и администриране на потребители.</summary>
    Manager = 2
}
