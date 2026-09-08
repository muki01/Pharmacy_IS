using System.Text.Json;
using PharmacyIS.Data.Infrastructure;

namespace PharmacyIS.UI.Infrastructure;

/// <summary>
/// Прочита настройките на приложението от файла <c>appsettings.json</c>,
/// разположен до изпълнимия файл. Ако файлът липсва или е повреден,
/// се използват стойности по подразбиране, за да може приложението
/// да стартира и да покаже смислено съобщение.
/// </summary>
public static class AppConfig
{
    private const string FileName = "appsettings.json";

    /// <summary>Настройки за връзка с базата от данни.</summary>
    public static DatabaseSettings Database { get; private set; } = new();

    /// <summary>Признак за зареждане на демонстрационни данни при първо стартиране.</summary>
    public static bool GenerateDemoData { get; private set; } = true;

    /// <summary>Съобщение за проблем при четене на конфигурацията, ако има такъв.</summary>
    public static string? LoadWarning { get; private set; }

    /// <summary>Зарежда конфигурацията.</summary>
    public static void Load()
    {
        var path = Path.Combine(AppContext.BaseDirectory, FileName);

        if (!File.Exists(path))
        {
            LoadWarning = $"Файлът „{FileName}“ не е намерен. Използват се настройки по подразбиране.";
            return;
        }

        try
        {
            using var stream = File.OpenRead(path);
            using var document = JsonDocument.Parse(stream, new JsonDocumentOptions
            {
                CommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            });

            if (!document.RootElement.TryGetProperty("Database", out var section))
            {
                LoadWarning = $"В „{FileName}“ липсва раздел „Database“. Използват се настройки по подразбиране.";
                return;
            }

            var settings = new DatabaseSettings();

            if (section.TryGetProperty("Provider", out var provider) &&
                Enum.TryParse<DatabaseProvider>(provider.GetString(), ignoreCase: true, out var parsed))
            {
                settings.Provider = parsed;
            }

            if (section.TryGetProperty("ConnectionString", out var connection))
                settings.ConnectionString = connection.GetString() ?? settings.ConnectionString;

            if (section.TryGetProperty("AutoCreateDatabase", out var autoCreate))
                settings.AutoCreateDatabase = autoCreate.GetBoolean();

            if (section.TryGetProperty("GenerateDemoData", out var demo))
                GenerateDemoData = demo.GetBoolean();

            // Относителният път до файловата база се превръща в абсолютен,
            // за да не зависи от текущата работна директория.
            if (settings.Provider == DatabaseProvider.Sqlite)
                settings.ConnectionString = ResolveSqlitePath(settings.ConnectionString);

            Database = settings;
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            LoadWarning = $"Файлът „{FileName}“ не може да бъде прочетен: {ex.Message}";
        }
    }

    /// <summary>Превръща относителния път към файловата база в абсолютен.</summary>
    private static string ResolveSqlitePath(string connectionString)
    {
        const string key = "Data Source=";
        var index = connectionString.IndexOf(key, StringComparison.OrdinalIgnoreCase);
        if (index < 0) return connectionString;

        var start = index + key.Length;
        var end = connectionString.IndexOf(';', start);
        var value = end < 0 ? connectionString[start..] : connectionString[start..end];

        if (Path.IsPathRooted(value)) return connectionString;

        var absolute = Path.Combine(AppContext.BaseDirectory, value.Trim());
        return connectionString.Remove(start, value.Length).Insert(start, absolute);
    }
}
