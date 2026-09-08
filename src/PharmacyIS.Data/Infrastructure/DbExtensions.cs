using System.Data.Common;

namespace PharmacyIS.Data.Infrastructure;

/// <summary>
/// Помощни разширения за работа с ADO.NET.
/// Добавянето на стойности става изключително чрез параметри на командата –
/// това е основната защита срещу атаки от тип SQL Injection, тъй като
/// стойността никога не се конкатенира към текста на заявката.
/// </summary>
public static class DbExtensions
{
    /// <summary>Добавя именуван параметър към командата.</summary>
    public static DbCommand AddParameter(this DbCommand command, string name, object? value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = NormalizeValue(value);
        command.Parameters.Add(parameter);
        return command;
    }

    /// <summary>Добавя списък от именувани параметри към командата.</summary>
    public static DbCommand AddParameters(this DbCommand command,
        params (string Name, object? Value)[] parameters)
    {
        foreach (var (name, value) in parameters)
            command.AddParameter(name, value);

        return command;
    }

    /// <summary>
    /// Привежда стойността до вид, приемлив и за двете СУБД:
    /// датите се записват в ISO формат, логическите стойности – като 0/1,
    /// а изброимите типове – като цяло число.
    /// </summary>
    private static object NormalizeValue(object? value) => value switch
    {
        null => DBNull.Value,
        DateTime dt => dt.ToString(SqlDialect.DateTimeFormat),
        bool b => b ? 1 : 0,
        Enum e => Convert.ToInt32(e),
        _ => value
    };

    // --- Безопасно четене на стойности от резултатното множество -----------
    // Различните доставчици връщат различни .NET типове за една и съща
    // колона (например INTEGER в SQLite и TINYINT в MySQL), затова
    // четенето минава през Convert.*, а не през строго типизираните методи.

    public static int GetInt(this DbDataReader reader, string column)
        => Convert.ToInt32(reader[column]);

    public static int? GetNullableInt(this DbDataReader reader, string column)
        => reader[column] is DBNull ? null : Convert.ToInt32(reader[column]);

    public static decimal GetDecimal(this DbDataReader reader, string column)
        => reader[column] is DBNull ? 0m : Convert.ToDecimal(reader[column]);

    public static string GetString(this DbDataReader reader, string column)
        => reader[column] is DBNull ? string.Empty : Convert.ToString(reader[column]) ?? string.Empty;

    public static string? GetNullableString(this DbDataReader reader, string column)
        => reader[column] is DBNull ? null : Convert.ToString(reader[column]);

    public static bool GetBool(this DbDataReader reader, string column)
        => reader[column] is not DBNull && Convert.ToInt32(reader[column]) != 0;

    public static DateTime GetDateTime(this DbDataReader reader, string column)
    {
        var value = reader[column];
        return value switch
        {
            DateTime dt => dt,
            string s => DateTime.Parse(s, System.Globalization.CultureInfo.InvariantCulture),
            _ => Convert.ToDateTime(value)
        };
    }

    public static DateTime? GetNullableDateTime(this DbDataReader reader, string column)
        => reader[column] is DBNull ? null : reader.GetDateTime(column);
}
