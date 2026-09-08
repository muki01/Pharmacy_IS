using PharmacyIS.Domain.Exceptions;
using PharmacyIS.Services.Models;
using Xunit;

namespace PharmacyIS.Tests;

/// <summary>
/// Тестове на основната бизнес операция – регистрирането на продажба.
/// Проверяват се както нормалните сценарии, така и граничните случаи.
/// </summary>
public class SalesServiceTests
{
    /// <summary>Зарежда една партида и връща идентификатора ѝ.</summary>
    private static void Deliver(TestDatabase test, int medicineId, string batchNumber,
        DateTime expiry, int quantity, decimal price)
    {
        test.Deliveries.RegisterDelivery(new DeliveryRequest
        {
            SupplierId = test.FirstSupplier().SupplierId,
            UserId = test.ManagerId,
            DeliveryDate = DateTime.Now,
            Lines =
            {
                new DeliveryRequestLine
                {
                    MedicineId = medicineId,
                    BatchNumber = batchNumber,
                    ExpiryDate = expiry,
                    Quantity = quantity,
                    UnitPrice = price
                }
            }
        });
    }

    [Fact(DisplayName = "Продажбата намалява наличността с продаденото количество")]
    public void RegisterSale_ReducesStock()
    {
        using var test = new TestDatabase();
        var medicine = test.FirstMedicine();

        Deliver(test, medicine.MedicineId, "П-001", DateTime.Today.AddYears(1), 100, 2.00m);

        test.Sales.RegisterSale(new SaleRequest
        {
            UserId = test.PharmacistId,
            Lines = { new SaleRequestLine(medicine.MedicineId, 7) }
        });

        var after = test.Catalog.GetMedicine(medicine.MedicineId)!;
        Assert.Equal(93, after.StockQuantity);
    }

    [Fact(DisplayName = "Сумата на продажбата се изчислява по продажната цена на продукта")]
    public void RegisterSale_CalculatesTotalFromCatalogPrice()
    {
        using var test = new TestDatabase();
        var medicine = test.FirstMedicine();

        Deliver(test, medicine.MedicineId, "П-002", DateTime.Today.AddYears(1), 50, 1.50m);

        var sale = test.Sales.RegisterSale(new SaleRequest
        {
            UserId = test.PharmacistId,
            Lines = { new SaleRequestLine(medicine.MedicineId, 3) }
        });

        Assert.Equal(Math.Round(medicine.Price * 3, 2), sale.TotalAmount);
        Assert.Equal(Math.Round(1.50m * 3, 2), sale.TotalCost);
        Assert.Equal(sale.TotalAmount - sale.TotalCost, sale.Profit);
    }

    [Fact(DisplayName = "Изписването започва от партидата с най-близък срок на годност (FEFO)")]
    public void RegisterSale_UsesFirstExpiredFirstOut()
    {
        using var test = new TestDatabase();
        var medicine = test.FirstMedicine();

        // Партидата с по-далечен срок се въвежда първа, за да се провери,
        // че подредбата се определя от срока, а не от реда на въвеждане.
        Deliver(test, medicine.MedicineId, "ДАЛЕЧНА", DateTime.Today.AddMonths(18), 20, 2.00m);
        Deliver(test, medicine.MedicineId, "БЛИЗКА", DateTime.Today.AddDays(30), 10, 1.00m);

        var sale = test.Sales.RegisterSale(new SaleRequest
        {
            UserId = test.PharmacistId,
            Lines = { new SaleRequestLine(medicine.MedicineId, 12) }
        });

        Assert.Equal(2, sale.Items.Count);

        var first = sale.Items[0];
        Assert.Equal("БЛИЗКА", first.BatchNumber);
        Assert.Equal(10, first.Quantity);

        var second = sale.Items[1];
        Assert.Equal("ДАЛЕЧНА", second.BatchNumber);
        Assert.Equal(2, second.Quantity);
    }

    [Fact(DisplayName = "Продажба над наличността се отказва и не променя склада")]
    public void RegisterSale_RejectsQuantityAboveStock()
    {
        using var test = new TestDatabase();
        var medicine = test.FirstMedicine();

        Deliver(test, medicine.MedicineId, "П-003", DateTime.Today.AddYears(1), 5, 2.00m);

        var error = Assert.Throws<InsufficientStockException>(() =>
            test.Sales.RegisterSale(new SaleRequest
            {
                UserId = test.PharmacistId,
                Lines = { new SaleRequestLine(medicine.MedicineId, 6) }
            }));

        Assert.Equal(6, error.Requested);
        Assert.Equal(5, error.Available);

        // Транзакцията е отменена – наличността е непроменена, документ не е създаден.
        Assert.Equal(5, test.Catalog.GetMedicine(medicine.MedicineId)!.StockQuantity);
        Assert.Empty(test.Sales.GetSales(test.Manager, DateTime.Today, DateTime.Today));
    }

    [Fact(DisplayName = "Партида с изтекъл срок не участва в продажба")]
    public void RegisterSale_IgnoresExpiredBatches()
    {
        using var test = new TestDatabase();
        var medicine = test.FirstMedicine();

        // Партида, изтекла преди един ден, зареждана чрез доставка отпреди месец.
        test.Deliveries.RegisterDelivery(new DeliveryRequest
        {
            SupplierId = test.FirstSupplier().SupplierId,
            UserId = test.ManagerId,
            DeliveryDate = DateTime.Today.AddMonths(-1),
            Lines =
            {
                new DeliveryRequestLine
                {
                    MedicineId = medicine.MedicineId,
                    BatchNumber = "ИЗТЕКЛА",
                    ExpiryDate = DateTime.Today.AddDays(-1),
                    Quantity = 40,
                    UnitPrice = 1.00m
                }
            }
        });

        var error = Assert.Throws<InsufficientStockException>(() =>
            test.Sales.RegisterSale(new SaleRequest
            {
                UserId = test.PharmacistId,
                Lines = { new SaleRequestLine(medicine.MedicineId, 1) }
            }));

        Assert.Equal(0, error.Available);

        // Количеството остава в склада – то подлежи на бракуване, не на продажба.
        Assert.Equal(40, test.Catalog.GetMedicine(medicine.MedicineId)!.StockQuantity);
    }

    [Fact(DisplayName = "Повтарящи се позиции се обединяват в една")]
    public void RegisterSale_MergesDuplicateLines()
    {
        using var test = new TestDatabase();
        var medicine = test.FirstMedicine();

        Deliver(test, medicine.MedicineId, "П-004", DateTime.Today.AddYears(1), 20, 2.00m);

        var sale = test.Sales.RegisterSale(new SaleRequest
        {
            UserId = test.PharmacistId,
            Lines =
            {
                new SaleRequestLine(medicine.MedicineId, 2),
                new SaleRequestLine(medicine.MedicineId, 3)
            }
        });

        Assert.Single(sale.Items);
        Assert.Equal(5, sale.Items[0].Quantity);
        Assert.Equal(15, test.Catalog.GetMedicine(medicine.MedicineId)!.StockQuantity);
    }

    [Theory(DisplayName = "Невалидни количества се отхвърлят")]
    [InlineData(0)]
    [InlineData(-3)]
    public void RegisterSale_RejectsNonPositiveQuantity(int quantity)
    {
        using var test = new TestDatabase();
        var medicine = test.FirstMedicine();

        Assert.Throws<ValidationException>(() =>
            test.Sales.RegisterSale(new SaleRequest
            {
                UserId = test.PharmacistId,
                Lines = { new SaleRequestLine(medicine.MedicineId, quantity) }
            }));
    }

    [Fact(DisplayName = "Празна продажба се отхвърля")]
    public void RegisterSale_RejectsEmptyCart()
    {
        using var test = new TestDatabase();

        Assert.Throws<ValidationException>(() =>
            test.Sales.RegisterSale(new SaleRequest { UserId = test.PharmacistId }));
    }

    [Fact(DisplayName = "Анулирането връща количествата в склада")]
    public void CancelSale_RestoresStock()
    {
        using var test = new TestDatabase();
        var medicine = test.FirstMedicine();

        Deliver(test, medicine.MedicineId, "П-005", DateTime.Today.AddYears(1), 30, 2.00m);

        var sale = test.Sales.RegisterSale(new SaleRequest
        {
            UserId = test.PharmacistId,
            Lines = { new SaleRequestLine(medicine.MedicineId, 8) }
        });

        Assert.Equal(22, test.Catalog.GetMedicine(medicine.MedicineId)!.StockQuantity);

        var manager = test.Auth.Login("manager", "manager123");
        test.Sales.CancelSale(manager, sale.SaleId);

        Assert.Equal(30, test.Catalog.GetMedicine(medicine.MedicineId)!.StockQuantity);
    }

    [Fact(DisplayName = "Фармацевт не може да анулира продажба")]
    public void CancelSale_RequiresManagerRole()
    {
        using var test = new TestDatabase();
        var medicine = test.FirstMedicine();

        Deliver(test, medicine.MedicineId, "П-006", DateTime.Today.AddYears(1), 10, 2.00m);

        var sale = test.Sales.RegisterSale(new SaleRequest
        {
            UserId = test.PharmacistId,
            Lines = { new SaleRequestLine(medicine.MedicineId, 1) }
        });

        var pharmacist = test.Auth.Login("farmacevt", "farmacevt123");

        Assert.Throws<AccessDeniedException>(() => test.Sales.CancelSale(pharmacist, sale.SaleId));
    }
}
