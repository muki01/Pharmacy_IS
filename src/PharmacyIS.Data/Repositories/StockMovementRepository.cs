using System.Data.Common;
using PharmacyIS.Data.Infrastructure;
using PharmacyIS.Domain.Entities;
using PharmacyIS.Domain.Enums;

namespace PharmacyIS.Data.Repositories;

/// <summary>
/// Достъп до журнала на складовите движения. Журналът е само за добавяне –
/// съществуващите записи никога не се променят и не се изтриват,
/// което го прави надежден източник за контрол на наличностите.
/// </summary>
public class StockMovementRepository
{
    private readonly PharmacyDb _db;

    public StockMovementRepository(PharmacyDb db) => _db = db;

    private const string SelectSql = """
        SELECT
            sm.MovementId, sm.MedicineId, sm.BatchId, sm.MovementType, sm.Quantity,
            sm.DocType, sm.DocNumber, sm.MovementDate, sm.UserId,
            m.Name AS MedicineName,
            b.BatchNumber,
            u.FullName AS UserName
        FROM StockMovements sm
        INNER JOIN Medicines m ON m.MedicineId = sm.MedicineId
        INNER JOIN Batches b ON b.BatchId = sm.BatchId
        INNER JOIN Users u ON u.UserId = sm.UserId
        """;

    /// <summary>Добавя запис в журнала.</summary>
    public int Insert(StockMovement movement)
    {
        _db.Execute("""
            INSERT INTO StockMovements
                (MedicineId, BatchId, MovementType, Quantity, DocType, DocNumber, MovementDate, UserId)
            VALUES
                (@medicineId, @batchId, @type, @quantity, @docType, @docNumber, @date, @userId);
            """,
            ("@medicineId", movement.MedicineId),
            ("@batchId", movement.BatchId),
            ("@type", (int)movement.MovementType),
            ("@quantity", movement.Quantity),
            ("@docType", movement.DocType),
            ("@docNumber", movement.DocNumber),
            ("@date", movement.MovementDate),
            ("@userId", movement.UserId));

        return _db.LastInsertId();
    }

    /// <summary>Движенията за период, по избор ограничени до един продукт.</summary>
    public List<StockMovement> GetForPeriod(DateTime from, DateTime to, int? medicineId = null)
    {
        var filter = medicineId is > 0 ? "AND sm.MedicineId = @medicineId" : string.Empty;

        var parameters = new List<(string, object?)>
        {
            ("@from", from.Date),
            ("@to", to.Date.AddDays(1).AddSeconds(-1))
        };

        if (medicineId is > 0)
            parameters.Add(("@medicineId", medicineId.Value));

        return _db.Query($"""
            {SelectSql}
            WHERE sm.MovementDate BETWEEN @from AND @to {filter}
            ORDER BY sm.MovementDate DESC, sm.MovementId DESC;
            """, Map, parameters.ToArray());
    }

    /// <summary>Движенията по конкретна партида.</summary>
    public List<StockMovement> GetForBatch(int batchId) => _db.Query(
        $"{SelectSql} WHERE sm.BatchId = @id ORDER BY sm.MovementDate, sm.MovementId;",
        Map, ("@id", batchId));

    /// <summary>
    /// Контролна проверка на наличностите: връща партидите, при които сумата
    /// от движенията в журнала се разминава с текущото количество.
    /// При коректна работа на системата резултатът е празен списък.
    /// </summary>
    public List<(int BatchId, string BatchNumber, string MedicineName, int Stored, int Calculated)> FindDiscrepancies()
        => _db.Query("""
            SELECT
                b.BatchId,
                b.BatchNumber,
                m.Name AS MedicineName,
                b.Quantity AS Stored,
                COALESCE((SELECT SUM(sm.Quantity) FROM StockMovements sm WHERE sm.BatchId = b.BatchId), 0) AS Calculated
            FROM Batches b
            INNER JOIN Medicines m ON m.MedicineId = b.MedicineId
            WHERE b.Quantity <> COALESCE(
                (SELECT SUM(sm.Quantity) FROM StockMovements sm WHERE sm.BatchId = b.BatchId), 0)
            ORDER BY m.Name;
            """,
            (DbDataReader r) => (
                r.GetInt("BatchId"),
                r.GetString("BatchNumber"),
                r.GetString("MedicineName"),
                r.GetInt("Stored"),
                r.GetInt("Calculated")));

    private static StockMovement Map(DbDataReader r) => new()
    {
        MovementId = r.GetInt("MovementId"),
        MedicineId = r.GetInt("MedicineId"),
        MedicineName = r.GetString("MedicineName"),
        BatchId = r.GetInt("BatchId"),
        BatchNumber = r.GetString("BatchNumber"),
        MovementType = (MovementType)r.GetInt("MovementType"),
        Quantity = r.GetInt("Quantity"),
        DocType = r.GetString("DocType"),
        DocNumber = r.GetString("DocNumber"),
        MovementDate = r.GetDateTime("MovementDate"),
        UserId = r.GetInt("UserId"),
        UserName = r.GetString("UserName")
    };
}
