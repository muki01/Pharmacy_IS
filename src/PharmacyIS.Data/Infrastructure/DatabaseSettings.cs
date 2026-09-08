namespace PharmacyIS.Data.Infrastructure;

/// <summary>
/// Настройки за връзка с базата от данни, прочетени от <c>appsettings.json</c>.
/// </summary>
public class DatabaseSettings
{
    /// <summary>Използвана СУБД.</summary>
    public DatabaseProvider Provider { get; set; } = DatabaseProvider.Sqlite;

    /// <summary>Низ за връзка, съответстващ на избраната СУБД.</summary>
    public string ConnectionString { get; set; } = "Data Source=pharmacy.db";

    /// <summary>
    /// При стойност <c>true</c> схемата и демонстрационните данни се създават
    /// автоматично при първо стартиране, ако базата е празна.
    /// </summary>
    public bool AutoCreateDatabase { get; set; } = true;
}
