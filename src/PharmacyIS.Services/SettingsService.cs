using PharmacyIS.Data.Infrastructure;
using PharmacyIS.Data.Repositories;
using PharmacyIS.Domain.Entities;
using PharmacyIS.Domain.Exceptions;
using PharmacyIS.Services.Models;

namespace PharmacyIS.Services;

/// <summary>
/// Системни настройки на търговския обект. Данните за аптеката се използват
/// в заглавната лента на приложението и върху издаваните документи, затова
/// се четат от всички роли, но се променят само от управителя.
/// </summary>
public class SettingsService
{
    private readonly DbSessionFactory _sessions;

    /// <summary>Наименование на аптеката.</summary>
    public const string PharmacyNameKey = "PharmacyName";

    /// <summary>Адрес на търговския обект.</summary>
    public const string PharmacyAddressKey = "PharmacyAddress";

    /// <summary>Ставка на данъка върху добавената стойност в проценти.</summary>
    public const string VatRateKey = "VatRate";

    public SettingsService(DbSessionFactory sessions) => _sessions = sessions;

    /// <summary>Данните за обекта, използвани в интерфейса и в документите.</summary>
    public PharmacyProfile GetProfile()
    {
        using var db = _sessions.Create();
        var repo = new NomenclatureRepository(db);

        return new PharmacyProfile
        {
            Name = repo.GetSetting(PharmacyNameKey, "Аптека"),
            Address = repo.GetSetting(PharmacyAddressKey, string.Empty),
            VatRate = repo.GetIntSetting(VatRateKey, 20)
        };
    }

    /// <summary>Записва данните за обекта. Достъпно само за управител.</summary>
    public void SaveProfile(User currentUser, PharmacyProfile profile)
    {
        AuthService.RequireManager(currentUser, "Промяна на системните настройки");
        ArgumentNullException.ThrowIfNull(profile);

        if (string.IsNullOrWhiteSpace(profile.Name))
            throw new ValidationException("Въведете наименование на аптеката.");

        if (profile.Name.Trim().Length > 120)
            throw new ValidationException("Наименованието не може да е по-дълго от 120 знака.");

        if (profile.Address?.Length > 200)
            throw new ValidationException("Адресът не може да е по-дълъг от 200 знака.");

        if (profile.VatRate is < 0 or > 100)
            throw new ValidationException("Ставката на ДДС трябва да е между 0 и 100 процента.");

        using var db = _sessions.Create();
        var repo = new NomenclatureRepository(db);

        repo.SetSetting(PharmacyNameKey, profile.Name.Trim());
        repo.SetSetting(PharmacyAddressKey, profile.Address?.Trim() ?? string.Empty);
        repo.SetSetting(VatRateKey, profile.VatRate.ToString());
    }

    /// <summary>
    /// Всички записани настройки заедно с описанията им. Използва се за
    /// извеждане на пълния списък в екрана за администриране.
    /// </summary>
    public List<SettingRow> GetAll(User currentUser)
    {
        AuthService.RequireManager(currentUser, "Преглед на системните настройки");

        using var db = _sessions.Create();

        return new NomenclatureRepository(db).GetAllSettings()
            .Select(s => new SettingRow
            {
                Key = s.Key,
                Value = s.Value,
                Description = s.Description ?? string.Empty
            })
            .ToList();
    }
}
