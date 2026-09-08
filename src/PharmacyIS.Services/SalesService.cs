using PharmacyIS.Domain;
using PharmacyIS.Data.Infrastructure;
using PharmacyIS.Data.Repositories;
using PharmacyIS.Domain.Entities;
using PharmacyIS.Domain.Enums;
using PharmacyIS.Domain.Exceptions;
using PharmacyIS.Services.Models;

namespace PharmacyIS.Services;

/// <summary>
/// Регистриране и преглед на продажбите – основната операция в аптеката.
///
/// Изписването на количествата се извършва по правилото FEFO
/// (First Expired, First Out): първо се изчерпва партидата с най-близък
/// срок на годност, което намалява бракуването на изтекли продукти.
/// Цялата операция се изпълнява в една транзакция, така че наличностите
/// и документът за продажба никога не могат да се разминат.
/// </summary>
public class SalesService
{
    private readonly DbSessionFactory _sessions;

    public SalesService(DbSessionFactory sessions) => _sessions = sessions;

    /// <summary>
    /// Регистрира продажба: създава документ, изписва количествата от партидите
    /// по правилото FEFO и записва движенията в складовия журнал.
    /// </summary>
    /// <returns>Записаният документ за продажба с попълнени редове и суми.</returns>
    /// <exception cref="ValidationException">При празна или невалидна заявка.</exception>
    /// <exception cref="InsufficientStockException">При недостатъчна наличност.</exception>
    public Sale RegisterSale(SaleRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var lines = NormalizeLines(request);

        using var db = _sessions.Create();
        var medicines = new MedicineRepository(db);
        var batches = new BatchRepository(db);
        var sales = new SaleRepository(db);
        var movements = new StockMovementRepository(db);

        var cashier = new UserRepository(db).GetById(request.UserId)
            ?? throw new ValidationException("Посоченият служител не съществува в системата.");

        db.BeginTransaction();
        try
        {
            var saleDate = request.SaleDate;
            var sale = new Sale
            {
                DocNumber = sales.NextDocNumber(saleDate),
                UserId = request.UserId,
                UserName = cashier.FullName,
                SaleDate = saleDate,
                PaymentType = request.PaymentType,
                Status = DocumentStatus.Completed
            };

            sale.SaleId = sales.InsertHeader(sale);

            decimal totalAmount = 0m;
            decimal totalCost = 0m;

            foreach (var line in lines)
            {
                var medicine = medicines.GetById(line.MedicineId)
                    ?? throw new ValidationException(
                        $"Продукт с идентификатор {line.MedicineId} не съществува в каталога.");

                if (!medicine.IsActive)
                    throw new ValidationException($"Продуктът „{medicine.Name}“ е спрян от продажба.");

                // Годни за продажба са само партидите с неизтекъл срок,
                // подредени по нарастващ срок на годност (FEFO).
                var available = batches.GetSellableBatches(medicine.MedicineId, saleDate);
                var totalAvailable = available.Sum(b => b.Quantity);

                if (totalAvailable < line.Quantity)
                    throw new InsufficientStockException(medicine.Name, line.Quantity, totalAvailable);

                var remaining = line.Quantity;

                foreach (var batch in available)
                {
                    if (remaining == 0) break;

                    var taken = Math.Min(remaining, batch.Quantity);

                    // Условното намаляване в базата е допълнителна защита при
                    // едновременна работа на две каси: ако друга операция вече
                    // е изчерпала партидата, редът не се променя и операцията се отменя.
                    var affected = batches.AdjustQuantity(batch.BatchId, -taken);
                    if (affected != 1)
                    {
                        throw new InsufficientStockException(
                            medicine.Name, line.Quantity, totalAvailable - remaining);
                    }

                    var item = new SaleItem
                    {
                        SaleId = sale.SaleId,
                        MedicineId = medicine.MedicineId,
                        MedicineCode = medicine.Code,
                        MedicineName = medicine.Name,
                        BatchId = batch.BatchId,
                        BatchNumber = batch.BatchNumber,
                        Quantity = taken,
                        UnitPrice = medicine.Price,
                        UnitCost = batch.PurchasePrice
                    };

                    item.SaleItemId = sales.InsertItem(item);
                    sale.Items.Add(item);

                    movements.Insert(new StockMovement
                    {
                        MedicineId = medicine.MedicineId,
                        BatchId = batch.BatchId,
                        MovementType = MovementType.Out,
                        Quantity = -taken,
                        DocType = "Продажба",
                        DocNumber = sale.DocNumber,
                        MovementDate = saleDate,
                        UserId = request.UserId
                    });

                    totalAmount += item.LineTotal;
                    totalCost += Math.Round(taken * batch.PurchasePrice, 2, MidpointRounding.AwayFromZero);
                    remaining -= taken;
                }
            }

            sale.TotalAmount = Math.Round(totalAmount, 2, MidpointRounding.AwayFromZero);
            sale.TotalCost = Math.Round(totalCost, 2, MidpointRounding.AwayFromZero);
            sales.UpdateTotals(sale.SaleId, sale.TotalAmount, sale.TotalCost);

            db.Commit();
            return sale;
        }
        catch
        {
            db.Rollback();
            throw;
        }
    }

    /// <summary>
    /// Анулира приключена продажба: връща изписаните количества в съответните
    /// партиди и записва сторниращи движения в складовия журнал.
    /// </summary>
    public void CancelSale(User currentUser, int saleId)
    {
        AuthService.RequireManager(currentUser, "Анулиране на продажба");

        using var db = _sessions.Create();
        var sales = new SaleRepository(db);
        var batches = new BatchRepository(db);
        var movements = new StockMovementRepository(db);

        db.BeginTransaction();
        try
        {
            var sale = sales.GetById(saleId)
                ?? throw new DomainException("Продажбата не е намерена.");

            if (sale.Status == DocumentStatus.Cancelled)
                throw new DomainException($"Продажба {sale.DocNumber} вече е анулирана.");

            foreach (var item in sale.Items)
            {
                batches.AdjustQuantity(item.BatchId, item.Quantity);

                movements.Insert(new StockMovement
                {
                    MedicineId = item.MedicineId,
                    BatchId = item.BatchId,
                    MovementType = MovementType.Reversal,
                    Quantity = item.Quantity,
                    DocType = "Сторно на продажба",
                    DocNumber = sale.DocNumber,
                    MovementDate = DateTime.Now,
                    UserId = currentUser.UserId
                });
            }

            sales.UpdateStatus(saleId, DocumentStatus.Cancelled);

            new AuditRepository(db).Log(currentUser.Username, "SALE_CANCELLED", true,
                $"Анулирана продажба {sale.DocNumber} на стойност {sale.TotalAmount:N2} {Money.Currency}");

            db.Commit();
        }
        catch
        {
            db.Rollback();
            throw;
        }
    }

    /// <summary>
    /// Продажбите за период. Служител без управленски права получава само
    /// собствените си документи: регистърът му служи за сверяване на касата
    /// в неговата смяна, а не за наблюдение на работата на колегите.
    /// </summary>
    public List<Sale> GetSales(User currentUser, DateTime from, DateTime to, int? userId = null)
    {
        ArgumentNullException.ThrowIfNull(currentUser);

        if (currentUser.Role != UserRole.Manager)
            userId = currentUser.UserId;

        using var db = _sessions.Create();
        return new SaleRepository(db).GetForPeriod(from, to, userId);
    }

    /// <summary>
    /// Един документ за продажба заедно с редовете му. Служител без
    /// управленски права има достъп само до собствените си документи.
    /// </summary>
    public Sale? GetSale(User currentUser, int saleId)
    {
        ArgumentNullException.ThrowIfNull(currentUser);

        using var db = _sessions.Create();
        var sale = new SaleRepository(db).GetById(saleId);

        if (sale is not null && currentUser.Role != UserRole.Manager &&
            sale.UserId != currentUser.UserId)
        {
            throw new AccessDeniedException("Преглед на чужд документ за продажба");
        }

        return sale;
    }

    /// <summary>
    /// Изчислява предварително сумата на продажбата, без да я записва.
    /// Използва се от касовия екран за показване на текущата сума.
    /// </summary>
    public decimal CalculateTotal(IEnumerable<(decimal UnitPrice, int Quantity)> lines)
        => Math.Round(lines.Sum(l => l.UnitPrice * l.Quantity), 2, MidpointRounding.AwayFromZero);

    /// <summary>
    /// Проверява заявката и обединява повтарящите се позиции, за да не се
    /// стигне до двойно изписване на един и същ продукт.
    /// </summary>
    private static List<SaleRequestLine> NormalizeLines(SaleRequest request)
    {
        if (request.Lines.Count == 0)
            throw new ValidationException("Продажбата не съдържа нито един продукт.");

        if (request.UserId <= 0)
            throw new ValidationException("Не е посочен служител, извършващ продажбата.");

        if (request.Lines.Any(l => l.Quantity <= 0))
            throw new ValidationException("Количеството на всяка позиция трябва да е положително число.");

        return request.Lines
            .GroupBy(l => l.MedicineId)
            .Select(g => new SaleRequestLine(g.Key, g.Sum(l => l.Quantity)))
            .ToList();
    }
}
