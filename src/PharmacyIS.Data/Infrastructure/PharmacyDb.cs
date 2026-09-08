using System.Data;
using System.Data.Common;
using Microsoft.Data.Sqlite;
using MySqlConnector;

namespace PharmacyIS.Data.Infrastructure;

/// <summary>
/// Сесия за работа с базата от данни (модел „Unit of Work“).
/// Обектът държи една отворена връзка и по избор една транзакция,
/// които се споделят от всички хранилища в рамките на една бизнес операция.
/// Това е механизмът, който гарантира, че продажбата и произтичащите от нея
/// изменения на наличностите се записват или изцяло, или изобщо.
/// </summary>
public sealed class PharmacyDb : IDisposable
{
    private readonly DbConnection _connection;
    private DbTransaction? _transaction;
    private bool _disposed;

    public DatabaseProvider Provider { get; }

    public PharmacyDb(DatabaseSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        Provider = settings.Provider;
        _connection = CreateConnection(settings);
        _connection.Open();

        if (Provider == DatabaseProvider.Sqlite)
        {
            // За SQLite вградената проверка на външните ключове е изключена
            // по подразбиране и трябва да се активира изрично за всяка връзка.
            using var pragma = _connection.CreateCommand();
            pragma.CommandText = "PRAGMA foreign_keys = ON;";
            pragma.ExecuteNonQuery();

            // Вградената функция LOWER на SQLite обхваща само латиницата.
            // Регистрираната тук функция преобразува и кирилицата, което
            // прави търсенето независимо от регистъра на буквите.
            ((SqliteConnection)_connection).CreateFunction(
                "LOWERU",
                (string? value) => value?.ToLowerInvariant(),
                isDeterministic: true);
        }
    }

    private static DbConnection CreateConnection(DatabaseSettings settings) => settings.Provider switch
    {
        DatabaseProvider.Sqlite => new SqliteConnection(settings.ConnectionString),
        DatabaseProvider.MySql => new MySqlConnection(settings.ConnectionString),
        _ => throw new NotSupportedException($"Неподдържана СУБД: {settings.Provider}")
    };

    /// <summary>Създава команда, свързана с текущата връзка и транзакция.</summary>
    public DbCommand CreateCommand(string sql)
    {
        var cmd = _connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.Transaction = _transaction;
        return cmd;
    }

    /// <summary>Стартира транзакция със зададеното ниво на изолация.</summary>
    public void BeginTransaction(IsolationLevel level = IsolationLevel.ReadCommitted)
    {
        if (_transaction is not null)
            throw new InvalidOperationException("Вече има стартирана транзакция.");

        _transaction = _connection.BeginTransaction(level);
    }

    /// <summary>Потвърждава транзакцията.</summary>
    public void Commit()
    {
        if (_transaction is null)
            throw new InvalidOperationException("Няма стартирана транзакция.");

        _transaction.Commit();
        _transaction.Dispose();
        _transaction = null;
    }

    /// <summary>Отменя транзакцията и връща базата в изходно състояние.</summary>
    public void Rollback()
    {
        if (_transaction is null) return;

        _transaction.Rollback();
        _transaction.Dispose();
        _transaction = null;
    }

    /// <summary>Връща първичния ключ на последния вмъкнат ред.</summary>
    public int LastInsertId()
    {
        using var cmd = CreateCommand(SqlDialect.LastInsertId(Provider));
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    /// <summary>Изпълнява заявка, която не връща резултатно множество.</summary>
    public int Execute(string sql, params (string Name, object? Value)[] parameters)
    {
        using var cmd = CreateCommand(sql);
        cmd.AddParameters(parameters);
        return cmd.ExecuteNonQuery();
    }

    /// <summary>Изпълнява заявка, която връща една скаларна стойност.</summary>
    public object? ExecuteScalar(string sql, params (string Name, object? Value)[] parameters)
    {
        using var cmd = CreateCommand(sql);
        cmd.AddParameters(parameters);
        return cmd.ExecuteScalar();
    }

    /// <summary>
    /// Изпълнява заявка и преобразува всеки ред от резултата чрез подадената функция.
    /// </summary>
    public List<T> Query<T>(string sql, Func<DbDataReader, T> map,
        params (string Name, object? Value)[] parameters)
    {
        using var cmd = CreateCommand(sql);
        cmd.AddParameters(parameters);

        var result = new List<T>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            result.Add(map(reader));

        return result;
    }

    /// <summary>Изпълнява заявка и връща първия ред или <c>null</c>, ако няма резултат.</summary>
    public T? QuerySingle<T>(string sql, Func<DbDataReader, T> map,
        params (string Name, object? Value)[] parameters) where T : class
    {
        var list = Query(sql, map, parameters);
        return list.Count > 0 ? list[0] : null;
    }

    /// <summary>Изпълнява скрипт, съставен от няколко израза, разделени с „;“.</summary>
    public void ExecuteScript(string script)
    {
        foreach (var statement in SplitStatements(script))
        {
            using var cmd = CreateCommand(statement);
            cmd.ExecuteNonQuery();
        }
    }

    /// <summary>
    /// Разделя SQL скрипт на отделни изрази. Празните редове и коментарите
    /// се пропускат, за да не се изпращат излишни заявки към сървъра.
    /// </summary>
    private static IEnumerable<string> SplitStatements(string script)
    {
        var lines = script.Replace("\r\n", "\n").Split('\n');
        var buffer = new System.Text.StringBuilder();

        foreach (var raw in lines)
        {
            var line = raw.TrimEnd();
            if (line.TrimStart().StartsWith("--", StringComparison.Ordinal)) continue;

            buffer.AppendLine(line);

            if (!line.TrimEnd().EndsWith(";", StringComparison.Ordinal)) continue;

            var statement = buffer.ToString().Trim();
            buffer.Clear();

            if (statement.Length > 1)
                yield return statement;
        }

        var tail = buffer.ToString().Trim();
        if (tail.Length > 0) yield return tail;
    }

    public void Dispose()
    {
        if (_disposed) return;

        // Незавършена транзакция при освобождаване се отменя, за да не
        // остане базата в противоречиво състояние.
        if (_transaction is not null)
        {
            try { _transaction.Rollback(); } catch { /* връзката вече е прекъсната */ }
            _transaction.Dispose();
            _transaction = null;
        }

        _connection.Dispose();
        _disposed = true;
    }
}
