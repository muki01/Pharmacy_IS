using PharmacyIS.Data.Infrastructure;
using PharmacyIS.Domain.Entities;
using PharmacyIS.Domain.Enums;
using PharmacyIS.Domain.Exceptions;
using PharmacyIS.Services.Models;

namespace PharmacyIS.Services;

/// <summary>
/// Генерира примерна история от доставки и продажби за последните три месеца.
///
/// Данните се създават чрез същите услуги, които обслужват и реалната работа,
/// затова наличностите, складовият журнал и справките остават напълно
/// съгласувани. Генераторът се използва за демонстрация на системата
/// и за тестване на справките с реалистичен обем данни.
/// </summary>
public class DemoDataGenerator
{
    private readonly DbSessionFactory _sessions;
    private readonly CatalogService _catalog;
    private readonly DeliveryService _deliveries;
    private readonly SalesService _sales;

    /// <summary>
    /// Постоянно начално число на генератора на случайни числа –
    /// осигурява еднакви демонстрационни данни при всяко създаване на базата.
    /// </summary>
    private const int RandomSeed = 20260430;

    public DemoDataGenerator(DbSessionFactory sessions)
    {
        _sessions = sessions;
        _catalog = new CatalogService(sessions);
        _deliveries = new DeliveryService(sessions);
        _sales = new SalesService(sessions);
    }

    /// <summary>
    /// Създава демонстрационната история, ако в базата още няма продажби.
    /// </summary>
    /// <returns><c>true</c>, ако данните са генерирани сега.</returns>
    public bool GenerateIfEmpty()
    {
        using (var db = _sessions.Create())
        {
            var salesCount = Convert.ToInt32(db.ExecuteScalar("SELECT COUNT(*) FROM Sales;"));
            if (salesCount > 0)
                return false;
        }

        Generate();
        return true;
    }

    /// <summary>Създава историята от доставки и продажби.</summary>
    public void Generate()
    {
        var random = new Random(RandomSeed);
        var today = DateTime.Today;

        var medicines = _catalog.GetMedicines();
        var suppliers = _catalog.GetSuppliers(onlyActive: true);

        if (medicines.Count == 0 || suppliers.Count == 0)
            throw new DomainException("Каталогът е празен – демонстрационни данни не могат да бъдат създадени.");

        var users = GetUserIds();
        var managerId = users.ManagerId;
        var pharmacists = users.PharmacistIds;

        // Събитията се подреждат по дата и се изпълняват в хронологичен ред,
        // за да не се стигне до продажба преди съответната доставка.
        var events = new List<DemoEvent>();

        // 1. Начално зареждане на склада преди 90 дни.
        events.Add(new DemoEvent(today.AddDays(-90).AddHours(9),
            () => CreateInitialDelivery(random, medicines, suppliers[0], managerId, today)));

        // 2. Текущи доставки на всеки седем дни.
        for (var day = 83; day >= 3; day -= 7)
        {
            var date = today.AddDays(-day).AddHours(random.Next(8, 11));
            var supplier = suppliers[random.Next(suppliers.Count)];
            events.Add(new DemoEvent(date,
                () => CreateRegularDelivery(random, medicines, supplier, managerId, date, today)));
        }

        // 3. Продажби за всеки работен ден през последните 85 дни.
        for (var day = 85; day >= 0; day--)
        {
            var date = today.AddDays(-day);
            if (date.DayOfWeek == DayOfWeek.Sunday) continue;

            var salesPerDay = random.Next(8, 22);
            for (var i = 0; i < salesPerDay; i++)
            {
                var moment = date.AddHours(random.Next(8, 19)).AddMinutes(random.Next(0, 60));
                var cashier = pharmacists[random.Next(pharmacists.Count)];
                events.Add(new DemoEvent(moment,
                    () => CreateSale(random, medicines, cashier, moment)));
            }
        }

        foreach (var demoEvent in events.OrderBy(e => e.Moment))
            demoEvent.Action();
    }

    // ------------------------------------------------------------------
    //  Отделни събития
    // ------------------------------------------------------------------

    /// <summary>Първоначално зареждане на склада с всички продукти от каталога.</summary>
    private void CreateInitialDelivery(Random random, List<Medicine> medicines,
        Supplier supplier, int userId, DateTime today)
    {
        var date = today.AddDays(-90).AddHours(9);
        var request = new DeliveryRequest
        {
            SupplierId = supplier.SupplierId,
            UserId = userId,
            DeliveryDate = date,
            InvoiceNumber = $"{random.Next(1000, 9999)}",
            Note = "Първоначално зареждане на склада"
        };

        foreach (var medicine in medicines)
        {
            // Част от партидите умишлено са с кратък срок на годност,
            // за да се демонстрира работата на предупрежденията и на брака.
            var expiry = random.Next(100) switch
            {
                < 6 => date.AddDays(random.Next(40, 80)),    // вече изтекъл към днешна дата
                < 16 => today.AddDays(random.Next(5, 55)),   // изтича скоро
                _ => today.AddMonths(random.Next(8, 30))
            };

            request.Lines.Add(new DeliveryRequestLine
            {
                MedicineId = medicine.MedicineId,
                BatchNumber = BuildBatchNumber(random, date),
                ExpiryDate = expiry.Date,
                Quantity = medicine.MinStock * random.Next(4, 9),
                UnitPrice = CalculatePurchasePrice(medicine.Price, random)
            });
        }

        _deliveries.RegisterDelivery(request);
    }

    /// <summary>Текуща доставка на част от асортимента.</summary>
    private void CreateRegularDelivery(Random random, List<Medicine> medicines,
        Supplier supplier, int userId, DateTime date, DateTime today)
    {
        var selected = medicines
            .OrderBy(_ => random.Next())
            .Take(random.Next(6, 14))
            .ToList();

        var request = new DeliveryRequest
        {
            SupplierId = supplier.SupplierId,
            UserId = userId,
            DeliveryDate = date,
            InvoiceNumber = $"{random.Next(1000, 9999)}",
            Note = "Текуща доставка"
        };

        foreach (var medicine in selected)
        {
            var expiry = random.Next(100) < 12
                ? today.AddDays(random.Next(10, 70))
                : today.AddMonths(random.Next(10, 32));

            request.Lines.Add(new DeliveryRequestLine
            {
                MedicineId = medicine.MedicineId,
                BatchNumber = BuildBatchNumber(random, date),
                ExpiryDate = expiry.Date,
                Quantity = medicine.MinStock * random.Next(2, 5),
                UnitPrice = CalculatePurchasePrice(medicine.Price, random)
            });
        }

        _deliveries.RegisterDelivery(request);
    }

    /// <summary>Една продажба с една до четири позиции.</summary>
    private void CreateSale(Random random, List<Medicine> medicines, int userId, DateTime moment)
    {
        var request = new SaleRequest
        {
            UserId = userId,
            SaleDate = moment,
            PaymentType = random.Next(100) < 65 ? PaymentType.Cash : PaymentType.Card
        };

        var lineCount = random.Next(1, 5);
        for (var i = 0; i < lineCount; i++)
        {
            var medicine = medicines[random.Next(medicines.Count)];
            request.Lines.Add(new SaleRequestLine(medicine.MedicineId, random.Next(1, 4)));
        }

        try
        {
            _sales.RegisterSale(request);
        }
        catch (InsufficientStockException)
        {
            // Изчерпана наличност в момента на генериране – продажбата се пропуска.
            // Същото поведение има и реалната система, когато продуктът свърши.
        }
    }

    // ------------------------------------------------------------------
    //  Помощни методи
    // ------------------------------------------------------------------

    /// <summary>Съставя партиден номер във формат „ГГММ-NNNN“.</summary>
    private static string BuildBatchNumber(Random random, DateTime date)
        => $"{date:yyMM}-{random.Next(1000, 9999)}";

    /// <summary>
    /// Изчислява доставна цена като продажната цена, намалена с търговска
    /// надценка между 22 % и 38 %.
    /// </summary>
    private static decimal CalculatePurchasePrice(decimal retailPrice, Random random)
    {
        var margin = 0.22m + random.Next(0, 17) / 100m;
        return Math.Round(retailPrice * (1 - margin), 2, MidpointRounding.AwayFromZero);
    }

    /// <summary>Взема идентификаторите на демонстрационните потребители.</summary>
    private (int ManagerId, List<int> PharmacistIds) GetUserIds()
    {
        using var db = _sessions.Create();
        var users = new Data.Repositories.UserRepository(db).GetAll();

        var manager = users.FirstOrDefault(u => u.Role == UserRole.Manager)
            ?? throw new DomainException("В базата липсва потребител с роля „Управител“.");

        var pharmacists = users.Where(u => u.Role == UserRole.Pharmacist).Select(u => u.UserId).ToList();
        if (pharmacists.Count == 0)
            pharmacists.Add(manager.UserId);

        return (manager.UserId, pharmacists);
    }

    /// <summary>Едно събитие от хронологията на демонстрационните данни.</summary>
    private sealed record DemoEvent(DateTime Moment, Action Action);
}
