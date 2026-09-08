namespace PharmacyIS.Data.Infrastructure;

/// <summary>
/// Създава сесии за работа с базата от данни. Предоставя се на слоя
/// с бизнес логика, за да не зависи той от конкретната СУБД и от
/// начина на изграждане на връзката.
/// </summary>
public class DbSessionFactory
{
    private readonly DatabaseSettings _settings;

    public DbSessionFactory(DatabaseSettings settings)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
    }

    /// <summary>Настройките, с които работи фабриката.</summary>
    public DatabaseSettings Settings => _settings;

    /// <summary>Отваря нова сесия. Извикващият е длъжен да я освободи.</summary>
    public PharmacyDb Create() => new(_settings);
}
