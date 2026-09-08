using System.Data.Common;
using PharmacyIS.Data.Infrastructure;
using PharmacyIS.Domain.Entities;
using PharmacyIS.Domain.Enums;

namespace PharmacyIS.Data.Repositories;

/// <summary>Достъп до документите за продажба и техните редове.</summary>
public class SaleRepository
{
    private readonly PharmacyDb _db;

    public SaleRepository(PharmacyDb db) => _db = db;

    /// <summary>Представка на номера на документите за продажба.</summary>
    public const string DocPrefix = "ПРД";

    private const string SelectSql = """
        SELECT
            s.SaleId, s.DocNumber, s.UserId, s.SaleDate, s.TotalAmount,
            s.TotalCost, s.PaymentType, s.Status,
            u.FullName AS UserName
        FROM Sales s
        INNER JOIN Users u ON u.UserId = s.UserId
        """;

    public Sale? GetById(int saleId)
    {
        var sale = _db.QuerySingle($"{SelectSql} WHERE s.SaleId = @id;", Map, ("@id", saleId));
        if (sale is not null)
            sale.Items = GetItems(saleId);

        return sale;
    }

    /// <summary>Продажбите за период, по избор филтрирани по служител.</summary>
    public List<Sale> GetForPeriod(DateTime from, DateTime to, int? userId = null)
    {
        var filter = userId is > 0 ? "AND s.UserId = @userId" : string.Empty;

        var parameters = new List<(string, object?)>
        {
            ("@from", from.Date),
            ("@to", to.Date.AddDays(1).AddSeconds(-1))
        };

        if (userId is > 0)
            parameters.Add(("@userId", userId.Value));

        return _db.Query($"""
            {SelectSql}
            WHERE s.SaleDate BETWEEN @from AND @to {filter}
            ORDER BY s.SaleDate DESC, s.SaleId DESC;
            """, Map, parameters.ToArray());
    }

    public List<SaleItem> GetItems(int saleId) => _db.Query("""
        SELECT
            si.SaleItemId, si.SaleId, si.MedicineId, si.BatchId,
            si.Quantity, si.UnitPrice, si.UnitCost,
            m.Code AS MedicineCode, m.Name AS MedicineName,
            b.BatchNumber
        FROM SaleItems si
        INNER JOIN Medicines m ON m.MedicineId = si.MedicineId
        INNER JOIN Batches b ON b.BatchId = si.BatchId
        WHERE si.SaleId = @id
        ORDER BY si.SaleItemId;
        """,
        (DbDataReader r) => new SaleItem
        {
            SaleItemId = r.GetInt("SaleItemId"),
            SaleId = r.GetInt("SaleId"),
            MedicineId = r.GetInt("MedicineId"),
            MedicineCode = r.GetString("MedicineCode"),
            MedicineName = r.GetString("MedicineName"),
            BatchId = r.GetInt("BatchId"),
            BatchNumber = r.GetString("BatchNumber"),
            Quantity = r.GetInt("Quantity"),
            UnitPrice = r.GetDecimal("UnitPrice"),
            UnitCost = r.GetDecimal("UnitCost")
        },
        ("@id", saleId));

    public int InsertHeader(Sale sale)
    {
        _db.Execute("""
            INSERT INTO Sales (DocNumber, UserId, SaleDate, TotalAmount, TotalCost, PaymentType, Status)
            VALUES (@docNumber, @userId, @date, @total, @cost, @payment, @status);
            """,
            ("@docNumber", sale.DocNumber),
            ("@userId", sale.UserId),
            ("@date", sale.SaleDate),
            ("@total", sale.TotalAmount),
            ("@cost", sale.TotalCost),
            ("@payment", (int)sale.PaymentType),
            ("@status", (int)sale.Status));

        return _db.LastInsertId();
    }

    public int InsertItem(SaleItem item)
    {
        _db.Execute("""
            INSERT INTO SaleItems (SaleId, MedicineId, BatchId, Quantity, UnitPrice, UnitCost, LineTotal)
            VALUES (@saleId, @medicineId, @batchId, @quantity, @unitPrice, @unitCost, @lineTotal);
            """,
            ("@saleId", item.SaleId),
            ("@medicineId", item.MedicineId),
            ("@batchId", item.BatchId),
            ("@quantity", item.Quantity),
            ("@unitPrice", item.UnitPrice),
            ("@unitCost", item.UnitCost),
            ("@lineTotal", item.LineTotal));

        return _db.LastInsertId();
    }

    public void UpdateTotals(int saleId, decimal total, decimal cost) => _db.Execute(
        "UPDATE Sales SET TotalAmount = @total, TotalCost = @cost WHERE SaleId = @id;",
        ("@id", saleId), ("@total", total), ("@cost", cost));

    public void UpdateStatus(int saleId, DocumentStatus status) => _db.Execute(
        "UPDATE Sales SET Status = @status WHERE SaleId = @id;",
        ("@id", saleId), ("@status", (int)status));

    /// <summary>Генерира следващия номер на документ във формат „ПРД-ГГГГ-NNNNNN“.</summary>
    public string NextDocNumber(DateTime date)
    {
        var prefix = $"{DocPrefix}-{date:yyyy}-";
        var count = Convert.ToInt32(_db.ExecuteScalar(
            "SELECT COUNT(*) FROM Sales WHERE DocNumber LIKE @prefix;",
            ("@prefix", prefix + "%")));

        return $"{prefix}{count + 1:D6}";
    }

    private static Sale Map(DbDataReader r) => new()
    {
        SaleId = r.GetInt("SaleId"),
        DocNumber = r.GetString("DocNumber"),
        UserId = r.GetInt("UserId"),
        UserName = r.GetString("UserName"),
        SaleDate = r.GetDateTime("SaleDate"),
        TotalAmount = r.GetDecimal("TotalAmount"),
        TotalCost = r.GetDecimal("TotalCost"),
        PaymentType = (PaymentType)r.GetInt("PaymentType"),
        Status = (DocumentStatus)r.GetInt("Status")
    };
}
