using System.Data.Common;
using PharmacyIS.Data.Infrastructure;
using PharmacyIS.Domain.Entities;
using PharmacyIS.Domain.Enums;

namespace PharmacyIS.Data.Repositories;

/// <summary>Достъп до документите за доставка и техните редове.</summary>
public class DeliveryRepository
{
    private readonly PharmacyDb _db;

    public DeliveryRepository(PharmacyDb db) => _db = db;

    /// <summary>Представка на номера на документите за доставка.</summary>
    public const string DocPrefix = "ДОС";

    private const string SelectSql = """
        SELECT
            d.DeliveryId, d.DocNumber, d.SupplierId, d.UserId, d.DeliveryDate,
            d.InvoiceNumber, d.TotalAmount, d.Status, d.Note,
            s.Name AS SupplierName,
            u.FullName AS UserName
        FROM Deliveries d
        INNER JOIN Suppliers s ON s.SupplierId = d.SupplierId
        INNER JOIN Users u ON u.UserId = d.UserId
        """;

    public Delivery? GetById(int deliveryId)
    {
        var delivery = _db.QuerySingle($"{SelectSql} WHERE d.DeliveryId = @id;", Map, ("@id", deliveryId));
        if (delivery is not null)
            delivery.Items = GetItems(deliveryId);

        return delivery;
    }

    /// <summary>Доставките за период, по избор филтрирани по доставчик.</summary>
    public List<Delivery> GetForPeriod(DateTime from, DateTime to, int? supplierId = null)
    {
        var filter = supplierId is > 0 ? "AND d.SupplierId = @supplierId" : string.Empty;

        var parameters = new List<(string, object?)>
        {
            ("@from", from.Date),
            ("@to", to.Date.AddDays(1).AddSeconds(-1))
        };

        if (supplierId is > 0)
            parameters.Add(("@supplierId", supplierId.Value));

        return _db.Query($"""
            {SelectSql}
            WHERE d.DeliveryDate BETWEEN @from AND @to {filter}
            ORDER BY d.DeliveryDate DESC, d.DeliveryId DESC;
            """, Map, parameters.ToArray());
    }

    public List<DeliveryItem> GetItems(int deliveryId) => _db.Query("""
        SELECT
            di.DeliveryItemId, di.DeliveryId, di.MedicineId, di.BatchNumber,
            di.ExpiryDate, di.Quantity, di.UnitPrice,
            m.Code AS MedicineCode, m.Name AS MedicineName
        FROM DeliveryItems di
        INNER JOIN Medicines m ON m.MedicineId = di.MedicineId
        WHERE di.DeliveryId = @id
        ORDER BY di.DeliveryItemId;
        """,
        (DbDataReader r) => new DeliveryItem
        {
            DeliveryItemId = r.GetInt("DeliveryItemId"),
            DeliveryId = r.GetInt("DeliveryId"),
            MedicineId = r.GetInt("MedicineId"),
            MedicineCode = r.GetString("MedicineCode"),
            MedicineName = r.GetString("MedicineName"),
            BatchNumber = r.GetString("BatchNumber"),
            ExpiryDate = r.GetDateTime("ExpiryDate"),
            Quantity = r.GetInt("Quantity"),
            UnitPrice = r.GetDecimal("UnitPrice")
        },
        ("@id", deliveryId));

    public int InsertHeader(Delivery delivery)
    {
        _db.Execute("""
            INSERT INTO Deliveries
                (DocNumber, SupplierId, UserId, DeliveryDate, InvoiceNumber, TotalAmount, Status, Note)
            VALUES
                (@docNumber, @supplierId, @userId, @date, @invoice, @total, @status, @note);
            """,
            ("@docNumber", delivery.DocNumber),
            ("@supplierId", delivery.SupplierId),
            ("@userId", delivery.UserId),
            ("@date", delivery.DeliveryDate),
            ("@invoice", delivery.InvoiceNumber),
            ("@total", delivery.TotalAmount),
            ("@status", (int)delivery.Status),
            ("@note", delivery.Note));

        return _db.LastInsertId();
    }

    public int InsertItem(DeliveryItem item)
    {
        _db.Execute("""
            INSERT INTO DeliveryItems
                (DeliveryId, MedicineId, BatchNumber, ExpiryDate, Quantity, UnitPrice, LineTotal)
            VALUES
                (@deliveryId, @medicineId, @batchNumber, @expiry, @quantity, @unitPrice, @lineTotal);
            """,
            ("@deliveryId", item.DeliveryId),
            ("@medicineId", item.MedicineId),
            ("@batchNumber", item.BatchNumber),
            ("@expiry", item.ExpiryDate.Date),
            ("@quantity", item.Quantity),
            ("@unitPrice", item.UnitPrice),
            ("@lineTotal", item.LineTotal));

        return _db.LastInsertId();
    }

    public void UpdateTotal(int deliveryId, decimal total) => _db.Execute(
        "UPDATE Deliveries SET TotalAmount = @total WHERE DeliveryId = @id;",
        ("@id", deliveryId), ("@total", total));

    /// <summary>
    /// Генерира следващия номер на документ във формат „ДОС-ГГГГ-NNNNNN“.
    /// Броенето е в рамките на календарната година.
    /// </summary>
    public string NextDocNumber(DateTime date)
    {
        var prefix = $"{DocPrefix}-{date:yyyy}-";
        var count = Convert.ToInt32(_db.ExecuteScalar(
            "SELECT COUNT(*) FROM Deliveries WHERE DocNumber LIKE @prefix;",
            ("@prefix", prefix + "%")));

        return $"{prefix}{count + 1:D6}";
    }

    private static Delivery Map(DbDataReader r) => new()
    {
        DeliveryId = r.GetInt("DeliveryId"),
        DocNumber = r.GetString("DocNumber"),
        SupplierId = r.GetInt("SupplierId"),
        SupplierName = r.GetString("SupplierName"),
        UserId = r.GetInt("UserId"),
        UserName = r.GetString("UserName"),
        DeliveryDate = r.GetDateTime("DeliveryDate"),
        InvoiceNumber = r.GetNullableString("InvoiceNumber"),
        TotalAmount = r.GetDecimal("TotalAmount"),
        Status = (DocumentStatus)r.GetInt("Status"),
        Note = r.GetNullableString("Note")
    };
}
