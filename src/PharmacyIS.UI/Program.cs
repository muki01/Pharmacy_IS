using System.Globalization;
using PharmacyIS.Data.Infrastructure;
using PharmacyIS.Services;
using PharmacyIS.UI.Forms;
using PharmacyIS.UI.Infrastructure;

namespace PharmacyIS.UI;

/// <summary>
/// Входна точка на приложението. Отговаря за прочитането на настройките,
/// подготовката на базата от данни и извеждането на екрана за вход.
/// </summary>
internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();

        // Датите и сумите се показват по българските правила
        // независимо от регионалните настройки на компютъра.
        var culture = CultureInfo.GetCultureInfo("bg-BG");
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;

        // Непрехванатите изключения се показват като съобщение, вместо
        // приложението да се затваря без обяснение.
        Application.ThreadException += (_, e) => UiHelpers.ShowError(null, e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is Exception ex) UiHelpers.ShowError(null, ex);
        };

        AppConfig.Load();

        if (AppConfig.LoadWarning is not null)
            UiHelpers.ShowWarning(null, AppConfig.LoadWarning, "Конфигурация");

        if (!PrepareDatabase())
            return;

        AppSession.Initialize(AppConfig.Database);

        // Главният прозорец се отваря пръв и веднага след това извежда
        // екрана за вход върху себе си.
        Application.Run(new MainForm());
    }

    /// <summary>
    /// Създава схемата и началните данни при първо стартиране.
    /// </summary>
    /// <returns><c>false</c>, ако базата не може да бъде подготвена.</returns>
    private static bool PrepareDatabase()
    {
        try
        {
            if (!AppConfig.Database.AutoCreateDatabase)
                return true;

            var initializer = new DatabaseInitializer(AppConfig.Database);
            var created = initializer.EnsureCreated();

            if (created && AppConfig.GenerateDemoData)
            {
                using var progress = new StartupProgressForm(
                    "Подготовка на базата от данни",
                    "Създават се демонстрационни доставки и продажби за последните три месеца…",
                    () => new DemoDataGenerator(new DbSessionFactory(AppConfig.Database)).GenerateIfEmpty());

                progress.ShowDialog();

                if (progress.Error is not null)
                    throw progress.Error;
            }

            return true;
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                "Базата от данни не може да бъде подготвена.\n\n" +
                $"Причина: {ex.Message}\n\n" +
                "Проверете настройките във файла appsettings.json.",
                "Грешка при стартиране", MessageBoxButtons.OK, MessageBoxIcon.Error);

            return false;
        }
    }
}
