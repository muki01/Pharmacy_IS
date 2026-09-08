using PharmacyIS.Data.Infrastructure;
using PharmacyIS.Services;

namespace PharmacyIS.Tests;

/// <summary>
/// Създава изолирана база от данни за нуждите на един тест.
/// Всеки тест работи върху собствен файл, който се изтрива след изпълнението,
/// така че тестовете не си влияят и могат да се изпълняват в произволен ред.
/// </summary>
public sealed class TestDatabase : IDisposable
{
    private readonly string _path;

    public DatabaseSettings Settings { get; }
    public DbSessionFactory Sessions { get; }

    public TestDatabase()
    {
        _path = Path.Combine(Path.GetTempPath(), $"pharmacy_test_{Guid.NewGuid():N}.db");

        Settings = new DatabaseSettings
        {
            Provider = DatabaseProvider.Sqlite,
            ConnectionString = $"Data Source={_path}",
            AutoCreateDatabase = true
        };

        new DatabaseInitializer(Settings).EnsureCreated();
        Sessions = new DbSessionFactory(Settings);
    }

    public AuthService Auth => new(Sessions);
    public CatalogService Catalog => new(Sessions);
    public SalesService Sales => new(Sessions);
    public DeliveryService Deliveries => new(Sessions);
    public StockService Stock => new(Sessions);
    public ReportService Reports => new(Sessions);
    public SettingsService SystemSettings => new(Sessions);

    /// <summary>Идентификатор на демонстрационния управител.</summary>
    public int ManagerId => GetUserId("manager");

    /// <summary>Идентификатор на демонстрационния фармацевт.</summary>
    public int PharmacistId => GetUserId("farmacevt");

    /// <summary>Демонстрационният управител като влязъл потребител.</summary>
    public Domain.Entities.User Manager => Auth.Login("manager", "manager123");

    /// <summary>Демонстрационният фармацевт като влязъл потребител.</summary>
    public Domain.Entities.User Pharmacist => Auth.Login("farmacevt", "farmacevt123");

    private int GetUserId(string username)
    {
        using var db = new PharmacyDb(Settings);
        return new Data.Repositories.UserRepository(db).GetByUsername(username)!.UserId;
    }

    /// <summary>Първият продукт от каталога, подреден по код.</summary>
    public Domain.Entities.Medicine FirstMedicine()
        => Catalog.GetMedicines().OrderBy(m => m.Code).First();

    /// <summary>Първият активен доставчик.</summary>
    public Domain.Entities.Supplier FirstSupplier()
        => Catalog.GetSuppliers(onlyActive: true).OrderBy(s => s.Code).First();

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

        try
        {
            if (File.Exists(_path)) File.Delete(_path);
        }
        catch (IOException)
        {
            // Файлът все още се използва от операционната система –
            // временната директория се почиства от системата.
        }
    }
}
