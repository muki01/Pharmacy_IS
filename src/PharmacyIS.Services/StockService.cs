using PharmacyIS.Data.Infrastructure;
using PharmacyIS.Data.Repositories;
using PharmacyIS.Domain.Entities;
using PharmacyIS.Domain.Enums;
using PharmacyIS.Domain.Exceptions;
using PharmacyIS.Services.Models;

namespace PharmacyIS.Services;

/// <summary>
/// Управление на складовите наличности: преглед на партидите, издаване на
/// предупреждения и бракуване на негодни количества.
/// </summary>
public class StockService
{
    private readonly DbSessionFactory _sessions;

    /// <summary>Ключ на настройката за брой дни преди изтичане на срока.</summary>
    public const string ExpiryWarningDaysKey = "ExpiryWarningDays";

    /// <summary>Стойност по подразбиране на прага за предупреждение, в дни.</summary>
    public const int DefaultExpiryWarningDays = 60;

    public StockService(DbSessionFactory sessions) => _sessions = sessions;

    /// <summary>Броят дни, при които системата предупреждава за изтичащ срок.</summary>
    public int GetExpiryWarningDays()
    {
        using var db = _sessions.Create();
        return new NomenclatureRepository(db).GetIntSetting(ExpiryWarningDaysKey, DefaultExpiryWarningDays);
    }

    /// <summary>Задава прага за предупреждение при изтичащ срок на годност.</summary>
    public void SetExpiryWarningDays(User currentUser, int days)
    {
        AuthService.RequireManager(currentUser, "Промяна на системните настройки");

        if (days is < 1 or > 365)
            throw new ValidationException("Прагът трябва да е между 1 и 365 дни.");

        using var db = _sessions.Create();
        new NomenclatureRepository(db).SetSetting(ExpiryWarningDaysKey, days.ToString());
    }

    /// <summary>
    /// Всички партиди с наличност. За роли без достъп до финансова информация
    /// доставната цена не се връща.
    /// </summary>
    public List<Batch> GetAvailableBatches(User currentUser)
    {
        using var db = _sessions.Create();
        return MaskPrices(currentUser, new BatchRepository(db).GetAllAvailable());
    }

    /// <summary>
    /// Партидите на конкретен продукт. За роли без достъп до финансова
    /// информация доставната цена не се връща.
    /// </summary>
    public List<Batch> GetBatches(User currentUser, int medicineId, bool onlyAvailable = false)
    {
        using var db = _sessions.Create();
        return MaskPrices(currentUser, new BatchRepository(db).GetByMedicine(medicineId, onlyAvailable));
    }

    /// <summary>
    /// Занулява доставните цени, когато ролята няма право да ги вижда.
    /// Филтрирането се извършва в слоя с бизнес логика, а не в интерфейса, за
    /// да не зависи защитата от това дали дадена колона е скрита на екрана.
    /// </summary>
    private static List<Batch> MaskPrices(User currentUser, List<Batch> batches)
    {
        if (AuthService.CanSeeFinancialData(currentUser))
            return batches;

        foreach (var batch in batches)
            batch.PurchasePrice = 0m;

        return batches;
    }

    /// <summary>
    /// Събира предупрежденията, които се показват при стартиране на програмата
    /// и при вход в модул „Склад“.
    /// </summary>
    public StockAlerts GetAlerts(User currentUser, DateTime? asOf = null)
    {
        var now = asOf ?? DateTime.Now;

        using var db = _sessions.Create();
        var nomenclature = new NomenclatureRepository(db);
        var reports = new ReportRepository(db);
        var batches = new BatchRepository(db);

        var warningDays = nomenclature.GetIntSetting(ExpiryWarningDaysKey, DefaultExpiryWarningDays);

        var lowStock = reports.GetStockReport(onlyBelowMinimum: true);
        var expiring = batches.GetExpiring(now, warningDays);

        // Предупрежденията носят и партидите, затова доставните цени се
        // прикриват по същото правило както навсякъде другаде.
        MaskPrices(currentUser, expiring);

        if (!AuthService.CanSeeFinancialData(currentUser))
            foreach (var row in lowStock) row.StockValue = 0m;

        return new StockAlerts
        {
            AsOf = now,
            ExpiryWarningDays = warningDays,
            LowStock = lowStock,
            Expired = expiring.Where(b => b.IsExpired(now)).ToList(),
            ExpiringSoon = expiring.Where(b => !b.IsExpired(now)).ToList()
        };
    }

    /// <summary>
    /// Съставя протокол за брак и изписва посочените количества от партидите.
    /// </summary>
    public WriteOff RegisterWriteOff(User currentUser, string reason,
        IEnumerable<(int BatchId, int Quantity)> lines, DateTime? date = null)
    {
        ArgumentNullException.ThrowIfNull(currentUser);

        var items = lines?.ToList() ?? throw new ArgumentNullException(nameof(lines));

        if (string.IsNullOrWhiteSpace(reason))
            throw new ValidationException("Въведете основание за брак.");

        if (items.Count == 0)
            throw new ValidationException("Протоколът за брак не съдържа нито една позиция.");

        if (items.Any(i => i.Quantity <= 0))
            throw new ValidationException("Количеството на всяка позиция трябва да е положително число.");

        var writeOffDate = date ?? DateTime.Now;

        using var db = _sessions.Create();
        var batchRepo = new BatchRepository(db);
        var writeOffs = new WriteOffRepository(db);
        var movements = new StockMovementRepository(db);

        db.BeginTransaction();
        try
        {
            var writeOff = new WriteOff
            {
                DocNumber = writeOffs.NextDocNumber(writeOffDate),
                UserId = currentUser.UserId,
                WriteOffDate = writeOffDate,
                Reason = reason.Trim(),
                Status = DocumentStatus.Completed
            };

            writeOff.WriteOffId = writeOffs.InsertHeader(writeOff);

            decimal totalCost = 0m;

            foreach (var (batchId, quantity) in items)
            {
                var batch = batchRepo.GetById(batchId)
                    ?? throw new ValidationException($"Партида с идентификатор {batchId} не съществува.");

                if (batch.Quantity < quantity)
                {
                    throw new InsufficientStockException(
                        $"{batch.MedicineName} (партида {batch.BatchNumber})", quantity, batch.Quantity);
                }

                var affected = batchRepo.AdjustQuantity(batchId, -quantity);
                if (affected != 1)
                {
                    throw new InsufficientStockException(
                        $"{batch.MedicineName} (партида {batch.BatchNumber})", quantity, batch.Quantity);
                }

                var item = new WriteOffItem
                {
                    WriteOffId = writeOff.WriteOffId,
                    MedicineId = batch.MedicineId,
                    MedicineCode = batch.MedicineCode,
                    MedicineName = batch.MedicineName,
                    BatchId = batch.BatchId,
                    BatchNumber = batch.BatchNumber,
                    ExpiryDate = batch.ExpiryDate,
                    Quantity = quantity,
                    UnitCost = batch.PurchasePrice
                };

                item.WriteOffItemId = writeOffs.InsertItem(item);
                writeOff.Items.Add(item);

                movements.Insert(new StockMovement
                {
                    MedicineId = batch.MedicineId,
                    BatchId = batch.BatchId,
                    MovementType = MovementType.WriteOff,
                    Quantity = -quantity,
                    DocType = "Брак",
                    DocNumber = writeOff.DocNumber,
                    MovementDate = writeOffDate,
                    UserId = currentUser.UserId
                });

                totalCost += item.LineTotal;
            }

            writeOff.TotalCost = Math.Round(totalCost, 2, MidpointRounding.AwayFromZero);
            writeOffs.UpdateTotal(writeOff.WriteOffId, writeOff.TotalCost);

            db.Commit();
            return writeOff;
        }
        catch
        {
            db.Rollback();
            throw;
        }
    }

    /// <summary>
    /// Бракува наведнъж всички партиди с изтекъл срок на годност.
    /// Използва се при периодичната проверка на склада.
    /// </summary>
    public WriteOff? WriteOffAllExpired(User currentUser, DateTime? asOf = null)
    {
        var now = asOf ?? DateTime.Now;

        // Списъкът се извлича в отделна, вече затворена сесия, за да не се
        // държат едновременно две връзки към една и съща файлова база.
        List<(int BatchId, int Quantity)> lines;
        using (var db = _sessions.Create())
        {
            lines = new BatchRepository(db)
                .GetExpiring(now, 0)
                .Where(b => b.IsExpired(now) && b.Quantity > 0)
                .Select(b => (b.BatchId, b.Quantity))
                .ToList();
        }

        if (lines.Count == 0)
            return null;

        return RegisterWriteOff(currentUser, "Изтекъл срок на годност", lines, now);
    }

    /// <summary>Протоколите за брак за период.</summary>
    public List<WriteOff> GetWriteOffs(DateTime from, DateTime to)
    {
        using var db = _sessions.Create();
        return new WriteOffRepository(db).GetForPeriod(from, to);
    }

    /// <summary>Складовите движения за период.</summary>
    public List<StockMovement> GetMovements(DateTime from, DateTime to, int? medicineId = null)
    {
        using var db = _sessions.Create();
        return new StockMovementRepository(db).GetForPeriod(from, to, medicineId);
    }

    /// <summary>Пълната история на движенията по една партида.</summary>
    public List<StockMovement> GetBatchMovements(int batchId)
    {
        using var db = _sessions.Create();
        return new StockMovementRepository(db).GetForBatch(batchId);
    }

    /// <summary>
    /// Контролна проверка: сравнява текущите количества по партидите със сумата
    /// от движенията в журнала. При коректна работа списъкът е празен.
    /// </summary>
    public List<(int BatchId, string BatchNumber, string MedicineName, int Stored, int Calculated)>
        CheckStockIntegrity()
    {
        using var db = _sessions.Create();
        return new StockMovementRepository(db).FindDiscrepancies();
    }
}
