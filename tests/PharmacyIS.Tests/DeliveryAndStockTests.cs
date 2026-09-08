using PharmacyIS.Domain.Exceptions;
using PharmacyIS.Services.Models;
using Xunit;

namespace PharmacyIS.Tests;

/// <summary>Тестове на доставките, склада и предупрежденията.</summary>
public class DeliveryAndStockTests
{
    private static DeliveryRequest BuildDelivery(TestDatabase test, int medicineId,
        string batchNumber, DateTime expiry, int quantity, decimal price, DateTime? date = null)
        => new()
        {
            SupplierId = test.FirstSupplier().SupplierId,
            UserId = test.ManagerId,
            DeliveryDate = date ?? DateTime.Now,
            InvoiceNumber = "1234",
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
        };

    [Fact(DisplayName = "Доставката увеличава наличността")]
    public void RegisterDelivery_IncreasesStock()
    {
        using var test = new TestDatabase();
        var medicine = test.FirstMedicine();

        Assert.Equal(0, test.Catalog.GetMedicine(medicine.MedicineId)!.StockQuantity);

        test.Deliveries.RegisterDelivery(
            BuildDelivery(test, medicine.MedicineId, "Д-001", DateTime.Today.AddYears(1), 60, 2.50m));

        Assert.Equal(60, test.Catalog.GetMedicine(medicine.MedicineId)!.StockQuantity);
    }

    [Fact(DisplayName = "Стойността на доставката е сума от редовете ѝ")]
    public void RegisterDelivery_CalculatesTotal()
    {
        using var test = new TestDatabase();
        var medicines = test.Catalog.GetMedicines().Take(2).ToList();

        var request = BuildDelivery(test, medicines[0].MedicineId, "Д-002",
            DateTime.Today.AddYears(1), 10, 3.00m);

        request.Lines.Add(new DeliveryRequestLine
        {
            MedicineId = medicines[1].MedicineId,
            BatchNumber = "Д-003",
            ExpiryDate = DateTime.Today.AddYears(1),
            Quantity = 4,
            UnitPrice = 2.50m
        });

        var delivery = test.Deliveries.RegisterDelivery(request);

        Assert.Equal(40.00m, delivery.TotalAmount);
    }

    [Fact(DisplayName = "Повторна доставка на съществуваща партида я допълва")]
    public void RegisterDelivery_TopsUpExistingBatch()
    {
        using var test = new TestDatabase();
        var medicine = test.FirstMedicine();
        var expiry = DateTime.Today.AddYears(1);

        test.Deliveries.RegisterDelivery(
            BuildDelivery(test, medicine.MedicineId, "ЕДНА", expiry, 10, 2.00m));
        test.Deliveries.RegisterDelivery(
            BuildDelivery(test, medicine.MedicineId, "ЕДНА", expiry, 15, 2.20m));

        var batches = test.Stock.GetBatches(test.Manager, medicine.MedicineId, onlyAvailable: true);

        Assert.Single(batches);
        Assert.Equal(25, batches[0].Quantity);
        Assert.Equal(2.20m, batches[0].PurchasePrice);
    }

    [Fact(DisplayName = "Доставка с изтекъл срок на годност се отхвърля")]
    public void RegisterDelivery_RejectsExpiredBatch()
    {
        using var test = new TestDatabase();
        var medicine = test.FirstMedicine();

        Assert.Throws<ValidationException>(() => test.Deliveries.RegisterDelivery(
            BuildDelivery(test, medicine.MedicineId, "СТАРА", DateTime.Today.AddDays(-1), 10, 2.00m)));

        Assert.Equal(0, test.Catalog.GetMedicine(medicine.MedicineId)!.StockQuantity);
    }

    [Fact(DisplayName = "Доставка без позиции се отхвърля")]
    public void RegisterDelivery_RejectsEmptyDocument()
    {
        using var test = new TestDatabase();

        Assert.Throws<ValidationException>(() => test.Deliveries.RegisterDelivery(new DeliveryRequest
        {
            SupplierId = test.FirstSupplier().SupplierId,
            UserId = test.ManagerId
        }));
    }

    [Fact(DisplayName = "Продукт под минималния запас попада в предупрежденията")]
    public void GetAlerts_ReportsLowStock()
    {
        using var test = new TestDatabase();
        var medicine = test.FirstMedicine();

        // Зарежда се количество, точно равно на минималния запас.
        test.Deliveries.RegisterDelivery(BuildDelivery(test, medicine.MedicineId, "Д-004",
            DateTime.Today.AddYears(1), medicine.MinStock, 2.00m));

        var alerts = test.Stock.GetAlerts(test.Manager);

        Assert.Contains(alerts.LowStock, r => r.MedicineCode == medicine.Code);
        Assert.True(alerts.HasAlerts);
    }

    [Fact(DisplayName = "Партида с наближаващ срок попада в предупрежденията")]
    public void GetAlerts_ReportsExpiringBatches()
    {
        using var test = new TestDatabase();
        var medicine = test.FirstMedicine();
        var warningDays = test.Stock.GetExpiryWarningDays();

        test.Deliveries.RegisterDelivery(BuildDelivery(test, medicine.MedicineId, "СКОРО",
            DateTime.Today.AddDays(warningDays - 5), 100, 2.00m));

        var alerts = test.Stock.GetAlerts(test.Manager);

        Assert.Contains(alerts.ExpiringSoon, b => b.BatchNumber == "СКОРО");
        Assert.Empty(alerts.Expired);
    }

    [Fact(DisplayName = "Бракуването изписва количеството от партидата")]
    public void RegisterWriteOff_ReducesStock()
    {
        using var test = new TestDatabase();
        var medicine = test.FirstMedicine();

        test.Deliveries.RegisterDelivery(BuildDelivery(test, medicine.MedicineId, "Б-001",
            DateTime.Today.AddYears(1), 40, 3.00m));

        var batch = test.Stock.GetBatches(test.Manager, medicine.MedicineId, onlyAvailable: true).Single();

        var writeOff = test.Stock.RegisterWriteOff(test.Manager, "Повредена опаковка",
            new[] { (batch.BatchId, 6) });

        Assert.Equal(18.00m, writeOff.TotalCost);
        Assert.Equal(34, test.Catalog.GetMedicine(medicine.MedicineId)!.StockQuantity);
    }

    [Fact(DisplayName = "Бракуване над наличността се отхвърля")]
    public void RegisterWriteOff_RejectsQuantityAboveStock()
    {
        using var test = new TestDatabase();
        var medicine = test.FirstMedicine();

        test.Deliveries.RegisterDelivery(BuildDelivery(test, medicine.MedicineId, "Б-002",
            DateTime.Today.AddYears(1), 5, 3.00m));

        var batch = test.Stock.GetBatches(test.Manager, medicine.MedicineId, onlyAvailable: true).Single();

        Assert.Throws<InsufficientStockException>(() =>
            test.Stock.RegisterWriteOff(test.Manager, "Проба", new[] { (batch.BatchId, 9) }));

        Assert.Equal(5, test.Catalog.GetMedicine(medicine.MedicineId)!.StockQuantity);
    }

    [Fact(DisplayName = "Груповото бракуване изчиства всички изтекли партиди")]
    public void WriteOffAllExpired_ClearsExpiredBatches()
    {
        using var test = new TestDatabase();
        var medicine = test.FirstMedicine();

        test.Deliveries.RegisterDelivery(BuildDelivery(test, medicine.MedicineId, "ИЗТЕКЛА",
            DateTime.Today.AddDays(-2), 12, 1.00m, DateTime.Today.AddMonths(-2)));

        test.Deliveries.RegisterDelivery(BuildDelivery(test, medicine.MedicineId, "ГОДНА",
            DateTime.Today.AddYears(1), 30, 1.00m));

        var writeOff = test.Stock.WriteOffAllExpired(test.Manager);

        Assert.NotNull(writeOff);
        Assert.Equal(12, writeOff!.Items.Sum(i => i.Quantity));
        Assert.Equal(30, test.Catalog.GetMedicine(medicine.MedicineId)!.StockQuantity);
        Assert.Empty(test.Stock.GetAlerts(test.Manager).Expired);
    }

    [Fact(DisplayName = "Складовият журнал съвпада с наличностите след поредица от операции")]
    public void StockLedger_MatchesQuantities()
    {
        using var test = new TestDatabase();
        var medicines = test.Catalog.GetMedicines().Take(3).ToList();

        foreach (var medicine in medicines)
        {
            test.Deliveries.RegisterDelivery(BuildDelivery(test, medicine.MedicineId,
                $"Ж-{medicine.MedicineId}", DateTime.Today.AddYears(1), 50, 2.00m));
        }

        for (var i = 0; i < 10; i++)
        {
            test.Sales.RegisterSale(new SaleRequest
            {
                UserId = test.PharmacistId,
                Lines = medicines.Select(m => new SaleRequestLine(m.MedicineId, 2)).ToList()
            });
        }

        var batch = test.Stock.GetBatches(test.Manager, medicines[0].MedicineId, onlyAvailable: true).Single();
        test.Stock.RegisterWriteOff(test.Manager, "Контролна проверка", new[] { (batch.BatchId, 4) });

        // Сумата от движенията в журнала трябва да съвпада с количествата по партидите.
        Assert.Empty(test.Stock.CheckStockIntegrity());
    }
}
