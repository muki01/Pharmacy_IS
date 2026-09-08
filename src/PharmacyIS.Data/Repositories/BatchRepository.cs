using System.Data.Common;
using PharmacyIS.Data.Infrastructure;
using PharmacyIS.Domain.Entities;

namespace PharmacyIS.Data.Repositories;

/// <summary>
/// Достъп до партидите – носителят на складовата наличност
/// и на срока на годност.
/// </summary>
public class BatchRepository
{
    private readonly PharmacyDb _db;

    public BatchRepository(PharmacyDb db) => _db = db;

    private const string SelectSql = """
        SELECT
            b.BatchId, b.MedicineId, b.BatchNumber, b.ExpiryDate, b.Quantity,
            b.PurchasePrice, b.CreatedAt,
            m.Name AS MedicineName, m.Code AS MedicineCode
        FROM Batches b
        INNER JOIN Medicines m ON m.MedicineId = b.MedicineId
        """;

    public Batch? GetById(int batchId)
        => _db.QuerySingle($"{SelectSql} WHERE b.BatchId = @id;", Map, ("@id", batchId));

    /// <summary>Всички партиди на даден продукт, подредени по срок на годност.</summary>
    public List<Batch> GetByMedicine(int medicineId, bool onlyAvailable = false)
    {
        var filter = onlyAvailable ? "AND b.Quantity > 0" : string.Empty;
        return _db.Query(
            $"{SelectSql} WHERE b.MedicineId = @id {filter} ORDER BY b.ExpiryDate;",
            Map, ("@id", medicineId));
    }

    /// <summary>
    /// Партидите, годни за продажба към подадената дата, подредени по правилото
    /// FEFO (First Expired, First Out) – първо се изписва партидата
    /// с най-близък срок на годност.
    /// </summary>
    public List<Batch> GetSellableBatches(int medicineId, DateTime asOf) => _db.Query(
        $"""
        {SelectSql}
        WHERE b.MedicineId = @id
          AND b.Quantity > 0
          AND b.ExpiryDate >= @asOf
        ORDER BY b.ExpiryDate, b.BatchId;
        """, Map, ("@id", medicineId), ("@asOf", asOf.Date));

    /// <summary>Всички налични партиди в склада.</summary>
    public List<Batch> GetAllAvailable() => _db.Query(
        $"{SelectSql} WHERE b.Quantity > 0 ORDER BY b.ExpiryDate, m.Name;", Map);

    /// <summary>
    /// Партиди с наличност, чийто срок изтича до подадения брой дни,
    /// както и вече изтеклите.
    /// </summary>
    public List<Batch> GetExpiring(DateTime asOf, int daysAhead) => _db.Query(
        $"""
        {SelectSql}
        WHERE b.Quantity > 0 AND b.ExpiryDate <= @limit
        ORDER BY b.ExpiryDate, m.Name;
        """, Map, ("@limit", asOf.Date.AddDays(daysAhead)));

    /// <summary>Намира партида по продукт, партиден номер и срок на годност.</summary>
    public Batch? Find(int medicineId, string batchNumber, DateTime expiryDate) => _db.QuerySingle(
        $"""
        {SelectSql}
        WHERE b.MedicineId = @id AND b.BatchNumber = @number AND b.ExpiryDate = @expiry;
        """, Map, ("@id", medicineId), ("@number", batchNumber), ("@expiry", expiryDate.Date));

    public int Insert(Batch batch)
    {
        _db.Execute("""
            INSERT INTO Batches (MedicineId, BatchNumber, ExpiryDate, Quantity, PurchasePrice, CreatedAt)
            VALUES (@medicineId, @number, @expiry, @quantity, @price, @createdAt);
            """,
            ("@medicineId", batch.MedicineId),
            ("@number", batch.BatchNumber),
            ("@expiry", batch.ExpiryDate.Date),
            ("@quantity", batch.Quantity),
            ("@price", batch.PurchasePrice),
            ("@createdAt", batch.CreatedAt));

        return _db.LastInsertId();
    }

    /// <summary>
    /// Променя наличността на партида с подадената разлика.
    /// Условието <c>Quantity + @delta &gt;= 0</c> е втора линия на защита –
    /// дори при грешка в горния слой базата не позволява отрицателна наличност.
    /// </summary>
    /// <returns>Брой засегнати редове: 1 при успех, 0 при недостатъчна наличност.</returns>
    public int AdjustQuantity(int batchId, int delta) => _db.Execute(
        """
        UPDATE Batches
        SET Quantity = Quantity + @delta
        WHERE BatchId = @id AND Quantity + @delta >= 0;
        """,
        ("@id", batchId), ("@delta", delta));

    /// <summary>Обновява доставната цена на партидата.</summary>
    public void UpdatePurchasePrice(int batchId, decimal price) => _db.Execute(
        "UPDATE Batches SET PurchasePrice = @price WHERE BatchId = @id;",
        ("@id", batchId), ("@price", price));

    /// <summary>Общо налично количество от продукта по всички партиди.</summary>
    public int GetTotalStock(int medicineId) => Convert.ToInt32(_db.ExecuteScalar(
        "SELECT COALESCE(SUM(Quantity), 0) FROM Batches WHERE MedicineId = @id;",
        ("@id", medicineId)));

    /// <summary>Годно за продажба количество към подадената дата.</summary>
    public int GetSellableStock(int medicineId, DateTime asOf) => Convert.ToInt32(_db.ExecuteScalar(
        "SELECT COALESCE(SUM(Quantity), 0) FROM Batches WHERE MedicineId = @id AND ExpiryDate >= @asOf;",
        ("@id", medicineId), ("@asOf", asOf.Date)));

    private static Batch Map(DbDataReader r) => new()
    {
        BatchId = r.GetInt("BatchId"),
        MedicineId = r.GetInt("MedicineId"),
        MedicineName = r.GetString("MedicineName"),
        MedicineCode = r.GetString("MedicineCode"),
        BatchNumber = r.GetString("BatchNumber"),
        ExpiryDate = r.GetDateTime("ExpiryDate"),
        Quantity = r.GetInt("Quantity"),
        PurchasePrice = r.GetDecimal("PurchasePrice"),
        CreatedAt = r.GetDateTime("CreatedAt")
    };
}
