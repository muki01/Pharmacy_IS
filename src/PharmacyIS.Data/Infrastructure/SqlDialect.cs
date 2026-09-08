namespace PharmacyIS.Data.Infrastructure;

/// <summary>
/// Капсулира различията в SQL диалектите на поддържаните СУБД.
/// Всички останали заявки в приложението са написани на съвместим
/// с двете системи синтаксис, така че тук се описват само разликите.
/// </summary>
public static class SqlDialect
{
    /// <summary>
    /// Функция за получаване на автоматично генерирания първичен ключ
    /// на последния вмъкнат ред.
    /// </summary>
    public static string LastInsertId(DatabaseProvider provider) => provider switch
    {
        DatabaseProvider.Sqlite => "SELECT last_insert_rowid();",
        DatabaseProvider.MySql => "SELECT LAST_INSERT_ID();",
        _ => throw new NotSupportedException($"Неподдържана СУБД: {provider}")
    };

    /// <summary>
    /// Израз за привеждане на текст към малки букви при сравнение без
    /// оглед на регистъра.
    ///
    /// Вградената функция LOWER на SQLite преобразува само буквите от
    /// латинската азбука – „АНАЛГИН“ остава непроменено и търсенето с малки
    /// букви не намира нищо. Затова при SQLite се използва регистрираната
    /// от приложението функция LOWERU, която работи с целия Уникод.
    /// При MySQL сравнението се извършва по подредбата utf8mb4_unicode_ci
    /// и вградената функция е достатъчна.
    /// </summary>
    public static string Lower(DatabaseProvider provider, string expression) => provider switch
    {
        DatabaseProvider.Sqlite => $"LOWERU({expression})",
        DatabaseProvider.MySql => $"LOWER({expression})",
        _ => throw new NotSupportedException($"Неподдържана СУБД: {provider}")
    };

    /// <summary>
    /// Знак за екраниране в условията LIKE. Използва се, за да могат
    /// символите % и _ да бъдат търсени буквално.
    /// </summary>
    public const string LikeEscape = "!";

    /// <summary>
    /// Екранира въведения от потребителя текст, преди да бъде използван като
    /// образец в LIKE. Без това търсенето на „50%“ би върнало всички редове.
    /// </summary>
    public static string EscapeLike(string term) => term
        .Replace(LikeEscape, LikeEscape + LikeEscape)
        .Replace("%", LikeEscape + "%")
        .Replace("_", LikeEscape + "_");

    /// <summary>
    /// Съставя условие LIKE без оглед на регистъра и с екраниране.
    /// </summary>
    public static string Like(DatabaseProvider provider, string expression, string parameter)
        => $"{Lower(provider, expression)} LIKE {parameter} ESCAPE '{LikeEscape}'";

    /// <summary>Име на вградения скрипт със схемата на базата за дадена СУБД.</summary>
    public static string SchemaResourceName(DatabaseProvider provider) => provider switch
    {
        DatabaseProvider.Sqlite => "PharmacyIS.Data.Scripts.schema_sqlite.sql",
        DatabaseProvider.MySql => "PharmacyIS.Data.Scripts.schema_mysql.sql",
        _ => throw new NotSupportedException($"Неподдържана СУБД: {provider}")
    };

    /// <summary>
    /// Формат за записване на дата и час. И в двете СУБД сравненията
    /// се извършват коректно върху този формат (ISO 8601).
    /// </summary>
    public const string DateTimeFormat = "yyyy-MM-dd HH:mm:ss";

    /// <summary>Формат за записване само на дата.</summary>
    public const string DateFormat = "yyyy-MM-dd";
}
