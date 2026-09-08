namespace PharmacyIS.Services.Models;

/// <summary>
/// Данни за търговския обект, които се показват в заглавната лента и се
/// отпечатват върху издаваните документи.
/// </summary>
public class PharmacyProfile
{
    /// <summary>Наименование на аптеката.</summary>
    public string Name { get; set; } = "Аптека";

    /// <summary>Адрес на обекта.</summary>
    public string Address { get; set; } = string.Empty;

    /// <summary>Ставка на данъка върху добавената стойност в проценти.</summary>
    public int VatRate { get; set; } = 20;
}

/// <summary>Ред от списъка със системни настройки.</summary>
public class SettingRow
{
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}
