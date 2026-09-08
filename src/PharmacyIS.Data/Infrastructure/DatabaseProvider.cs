namespace PharmacyIS.Data.Infrastructure;

/// <summary>
/// Поддържани системи за управление на бази от данни.
/// Приложението работи с абстрактния интерфейс на ADO.NET
/// (<c>System.Data.Common</c>), което позволява смяна на СУБД
/// само чрез промяна на конфигурационния файл.
/// </summary>
public enum DatabaseProvider
{
    /// <summary>Вградена файлова база данни – не изисква инсталиран сървър.</summary>
    Sqlite = 1,

    /// <summary>Клиент-сървърна СУБД MySQL / MariaDB.</summary>
    MySql = 2
}
