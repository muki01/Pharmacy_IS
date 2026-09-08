using System.Data.Common;
using PharmacyIS.Data.Infrastructure;
using PharmacyIS.Domain.Entities;
using PharmacyIS.Domain.Enums;

namespace PharmacyIS.Data.Repositories;

/// <summary>Достъп до протоколите за брак и техните редове.</summary>
public class WriteOffRepository
{
    private readonly PharmacyDb _db;

    public WriteOffRepository(PharmacyDb db) => _db = db;

    /// <summary>Представка на номера на протоколите за брак.</summary>
    public const string DocPrefix = "БРК";

    private const string SelectSql = """
        SELECT
            w.WriteOffId, w.DocNumber, w.UserId, w.WriteOffDate,
            w.Reason, w.TotalCost, w.Status,
            u.FullName AS UserName
        FROM WriteOffs w
        INNER JOIN Users u ON u.UserId = w.UserId
        """;

    public WriteOff? GetById(int writeOffId)
    {
        var writeOff = _db.QuerySingle($"{SelectSql} WHERE w.WriteOffId = @id;", Map, ("@id", writeOffId));
        if (writeOff is not null)
            writeOff.Items = GetItems(writeOffId);

        return writeOff;
    }

    public List<WriteOff> GetForPeriod(DateTime from, DateTime to) => _db.Query($"""
        {SelectSql}
        WHERE w.WriteOffDate BETWEEN @from AND @to
        ORDER BY w.WriteOffDate DESC, w.WriteOffId DESC;
        """, Map,
        ("@from", from.Date),
        ("@to", to.Date.AddDays(1).AddSeconds(-1)));

    public List<WriteOffItem> GetItems(int writeOffId) => _db.Query("""
        SELECT
            wi.WriteOffItemId, wi.WriteOffId, wi.MedicineId, wi.BatchId,
            wi.Quantity, wi.UnitCost,
            m.Code AS MedicineCode, m.Name AS MedicineName,
            b.BatchNumber, b.ExpiryDate
        FROM WriteOffItems wi
        INNER JOIN Medicines m ON m.MedicineId = wi.MedicineId
        INNER JOIN Batches b ON b.BatchId = wi.BatchId
        WHERE wi.WriteOffId = @id
        ORDER BY wi.WriteOffItemId;
        """,
        (DbDataReader r) => new WriteOffItem
        {
            WriteOffItemId = r.GetInt("WriteOffItemId"),
            WriteOffId = r.GetInt("WriteOffId"),
            MedicineId = r.GetInt("MedicineId"),
            MedicineCode = r.GetString("MedicineCode"),
            MedicineName = r.GetString("MedicineName"),
            BatchId = r.GetInt("BatchId"),
            BatchNumber = r.GetString("BatchNumber"),
            ExpiryDate = r.GetDateTime("ExpiryDate"),
            Quantity = r.GetInt("Quantity"),
            UnitCost = r.GetDecimal("UnitCost")
        },
        ("@id", writeOffId));

    public int InsertHeader(WriteOff writeOff)
    {
        _db.Execute("""
            INSERT INTO WriteOffs (DocNumber, UserId, WriteOffDate, Reason, TotalCost, Status)
            VALUES (@docNumber, @userId, @date, @reason, @cost, @status);
            """,
            ("@docNumber", writeOff.DocNumber),
            ("@userId", writeOff.UserId),
            ("@date", writeOff.WriteOffDate),
            ("@reason", writeOff.Reason),
            ("@cost", writeOff.TotalCost),
            ("@status", (int)writeOff.Status));

        return _db.LastInsertId();
    }

    public int InsertItem(WriteOffItem item)
    {
        _db.Execute("""
            INSERT INTO WriteOffItems (WriteOffId, MedicineId, BatchId, Quantity, UnitCost, LineTotal)
            VALUES (@writeOffId, @medicineId, @batchId, @quantity, @unitCost, @lineTotal);
            """,
            ("@writeOffId", item.WriteOffId),
            ("@medicineId", item.MedicineId),
            ("@batchId", item.BatchId),
            ("@quantity", item.Quantity),
            ("@unitCost", item.UnitCost),
            ("@lineTotal", item.LineTotal));

        return _db.LastInsertId();
    }

    public void UpdateTotal(int writeOffId, decimal totalCost) => _db.Execute(
        "UPDATE WriteOffs SET TotalCost = @cost WHERE WriteOffId = @id;",
        ("@id", writeOffId), ("@cost", totalCost));

    /// <summary>Генерира следващия номер на документ във формат „БРК-ГГГГ-NNNNNN“.</summary>
    public string NextDocNumber(DateTime date)
    {
        var prefix = $"{DocPrefix}-{date:yyyy}-";
        var count = Convert.ToInt32(_db.ExecuteScalar(
            "SELECT COUNT(*) FROM WriteOffs WHERE DocNumber LIKE @prefix;",
            ("@prefix", prefix + "%")));

        return $"{prefix}{count + 1:D6}";
    }

    private static WriteOff Map(DbDataReader r) => new()
    {
        WriteOffId = r.GetInt("WriteOffId"),
        DocNumber = r.GetString("DocNumber"),
        UserId = r.GetInt("UserId"),
        UserName = r.GetString("UserName"),
        WriteOffDate = r.GetDateTime("WriteOffDate"),
        Reason = r.GetString("Reason"),
        TotalCost = r.GetDecimal("TotalCost"),
        Status = (DocumentStatus)r.GetInt("Status")
    };
}
