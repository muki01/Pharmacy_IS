using PharmacyIS.Data.Infrastructure;
using PharmacyIS.Domain.Entities;
using PharmacyIS.Domain.Enums;
using PharmacyIS.Services;
using PharmacyIS.Services.Models;

namespace PharmacyIS.UI.Infrastructure;

/// <summary>
/// Състояние на текущата работна сесия: влезлият потребител и обектите
/// със стопанска логика, които обслужват екраните. Създава се веднъж
/// при стартиране на приложението.
/// </summary>
public static class AppSession
{
    private static DbSessionFactory? _sessions;

    /// <summary>Потребител, влязъл в системата.</summary>
    public static User? CurrentUser { get; private set; }

    public static AuthService Auth { get; private set; } = null!;
    public static CatalogService Catalog { get; private set; } = null!;
    public static SalesService Sales { get; private set; } = null!;
    public static DeliveryService Deliveries { get; private set; } = null!;
    public static StockService Stock { get; private set; } = null!;
    public static ReportService Reports { get; private set; } = null!;
    public static SettingsService Settings { get; private set; } = null!;

    /// <summary>Данни за обекта – заглавия, документи и ставка на ДДС.</summary>
    public static PharmacyProfile Pharmacy { get; private set; } = new();

    /// <summary>Наименование на аптеката, използвано в заглавията и документите.</summary>
    public static string PharmacyName => Pharmacy.Name;

    /// <summary>Влезлият потребител има роля „Управител“.</summary>
    public static bool IsManager => CurrentUser?.Role == UserRole.Manager;

    /// <summary>Инициализира услугите за подадените настройки на базата.</summary>
    public static void Initialize(DatabaseSettings settings)
    {
        _sessions = new DbSessionFactory(settings);

        Auth = new AuthService(_sessions);
        Catalog = new CatalogService(_sessions);
        Sales = new SalesService(_sessions);
        Deliveries = new DeliveryService(_sessions);
        Stock = new StockService(_sessions);
        Reports = new ReportService(_sessions);
        Settings = new SettingsService(_sessions);

        ReloadPharmacyProfile();
    }

    /// <summary>
    /// Презарежда данните за обекта. Извиква се при стартиране и след
    /// промяна на системните настройки.
    /// </summary>
    public static void ReloadPharmacyProfile() => Pharmacy = Settings.GetProfile();

    /// <summary>Записва влезлия потребител.</summary>
    public static void SignIn(User user) => CurrentUser = user;

    /// <summary>Прекратява сесията на текущия потребител.</summary>
    public static void SignOut() => CurrentUser = null;

    /// <summary>Фабриката за сесии – използва се от помощните екрани.</summary>
    public static DbSessionFactory Sessions =>
        _sessions ?? throw new InvalidOperationException("Сесията не е инициализирана.");
}
