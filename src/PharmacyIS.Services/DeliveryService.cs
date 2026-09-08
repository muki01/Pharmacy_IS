using PharmacyIS.Data.Infrastructure;
using PharmacyIS.Data.Repositories;
using PharmacyIS.Domain.Entities;
using PharmacyIS.Domain.Enums;
using PharmacyIS.Domain.Exceptions;
using PharmacyIS.Services.Models;

namespace PharmacyIS.Services;

/// <summary>
/// Заприхождаване на доставки от доставчици.
///
/// Всяка доставена позиция постъпва в партида, определена от продукта,
/// партидния номер и срока на годност. Ако такава партида вече съществува,
/// количеството ѝ се увеличава; в противен случай се създава нова.
///
/// Заприхождаването е ежедневна складова операция и е достъпно и за двете
/// роли – стоката пристига по всяко време на работния ден и се приема от
/// служителя на смяна, който подписва приемателния документ. Прегледът на
/// регистъра на доставките показва стойностите на всички документи и затова
/// е запазен за управителя.
/// </summary>
public class DeliveryService
{
    private readonly DbSessionFactory _sessions;

    public DeliveryService(DbSessionFactory sessions) => _sessions = sessions;

    /// <summary>Регистрира доставка и увеличава наличностите.</summary>
    /// <returns>Записаният документ за доставка.</returns>
    public Delivery RegisterDelivery(DeliveryRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        Validate(request);

        using var db = _sessions.Create();
        var medicines = new MedicineRepository(db);
        var batches = new BatchRepository(db);
        var deliveries = new DeliveryRepository(db);
        var movements = new StockMovementRepository(db);
        var suppliers = new SupplierRepository(db);

        db.BeginTransaction();
        try
        {
            var supplier = suppliers.GetById(request.SupplierId)
                ?? throw new ValidationException("Посоченият доставчик не съществува.");

            if (!supplier.IsActive)
                throw new ValidationException($"Доставчик „{supplier.Name}“ е деактивиран.");

            var delivery = new Delivery
            {
                DocNumber = deliveries.NextDocNumber(request.DeliveryDate),
                SupplierId = supplier.SupplierId,
                SupplierName = supplier.Name,
                UserId = request.UserId,
                DeliveryDate = request.DeliveryDate,
                InvoiceNumber = request.InvoiceNumber,
                Note = request.Note,
                Status = DocumentStatus.Completed
            };

            delivery.DeliveryId = deliveries.InsertHeader(delivery);

            decimal total = 0m;

            foreach (var line in request.Lines)
            {
                var medicine = medicines.GetById(line.MedicineId)
                    ?? throw new ValidationException(
                        $"Продукт с идентификатор {line.MedicineId} не съществува в каталога.");

                // Партидата се търси по продукт, номер и срок на годност.
                var batch = batches.Find(medicine.MedicineId, line.BatchNumber, line.ExpiryDate);

                if (batch is null)
                {
                    batch = new Batch
                    {
                        MedicineId = medicine.MedicineId,
                        BatchNumber = line.BatchNumber,
                        ExpiryDate = line.ExpiryDate.Date,
                        Quantity = line.Quantity,
                        PurchasePrice = line.UnitPrice,
                        CreatedAt = request.DeliveryDate
                    };

                    batch.BatchId = batches.Insert(batch);
                }
                else
                {
                    batches.AdjustQuantity(batch.BatchId, line.Quantity);

                    // Актуализира се доставната цена, за да отговаря
                    // себестойността на последната действителна доставка.
                    batches.UpdatePurchasePrice(batch.BatchId, line.UnitPrice);
                }

                var item = new DeliveryItem
                {
                    DeliveryId = delivery.DeliveryId,
                    MedicineId = medicine.MedicineId,
                    MedicineCode = medicine.Code,
                    MedicineName = medicine.Name,
                    BatchNumber = line.BatchNumber,
                    ExpiryDate = line.ExpiryDate.Date,
                    Quantity = line.Quantity,
                    UnitPrice = line.UnitPrice
                };

                item.DeliveryItemId = deliveries.InsertItem(item);
                delivery.Items.Add(item);

                movements.Insert(new StockMovement
                {
                    MedicineId = medicine.MedicineId,
                    BatchId = batch.BatchId,
                    MovementType = MovementType.In,
                    Quantity = line.Quantity,
                    DocType = "Доставка",
                    DocNumber = delivery.DocNumber,
                    MovementDate = request.DeliveryDate,
                    UserId = request.UserId
                });

                total += item.LineTotal;
            }

            delivery.TotalAmount = Math.Round(total, 2, MidpointRounding.AwayFromZero);
            deliveries.UpdateTotal(delivery.DeliveryId, delivery.TotalAmount);

            db.Commit();
            return delivery;
        }
        catch
        {
            db.Rollback();
            throw;
        }
    }

    /// <summary>Доставките за период. Достъпно само за управител.</summary>
    public List<Delivery> GetDeliveries(User currentUser, DateTime from, DateTime to,
        int? supplierId = null)
    {
        AuthService.RequireManager(currentUser, "Преглед на регистъра на доставките");

        using var db = _sessions.Create();
        return new DeliveryRepository(db).GetForPeriod(from, to, supplierId);
    }

    /// <summary>
    /// Един документ за доставка заедно с редовете му. Достъпно само за
    /// управител – документът съдържа доставни цени и стойност.
    /// </summary>
    public Delivery? GetDelivery(User currentUser, int deliveryId)
    {
        AuthService.RequireManager(currentUser, "Преглед на документ за доставка");

        using var db = _sessions.Create();
        return new DeliveryRepository(db).GetById(deliveryId);
    }

    /// <summary>Проверява заявката за доставка преди записването ѝ.</summary>
    private static void Validate(DeliveryRequest request)
    {
        if (request.SupplierId <= 0)
            throw new ValidationException("Изберете доставчик.");

        if (request.UserId <= 0)
            throw new ValidationException("Не е посочен служител, приел доставката.");

        if (request.Lines.Count == 0)
            throw new ValidationException("Доставката не съдържа нито един продукт.");

        foreach (var line in request.Lines)
        {
            if (line.MedicineId <= 0)
                throw new ValidationException("Всеки ред трябва да съдържа избран продукт.");

            if (string.IsNullOrWhiteSpace(line.BatchNumber))
                throw new ValidationException("Въведете партиден номер за всеки ред от доставката.");

            if (line.Quantity <= 0)
                throw new ValidationException("Количеството на всяка позиция трябва да е положително число.");

            // Нулева доставна цена би направила себестойността нула и би
            // завишила отчетената печалба, затова не се приема.
            if (line.UnitPrice <= 0)
                throw new ValidationException(
                    "Въведете доставна цена, по-голяма от нула, за всяка позиция.");

            if (line.UnitPrice > 100_000)
                throw new ValidationException("Въведената доставна цена е извън допустимите граници.");

            // Не се допуска заприхождаване на продукт с изтекъл срок на годност.
            if (line.ExpiryDate.Date <= request.DeliveryDate.Date)
            {
                throw new ValidationException(
                    $"Срокът на годност на партида {line.BatchNumber} " +
                    $"({line.ExpiryDate:dd.MM.yyyy}) е изтекъл или изтича в деня на доставката.");
            }
        }
    }
}
