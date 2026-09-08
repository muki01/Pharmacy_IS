using System.Data.Common;
using PharmacyIS.Data.Infrastructure;
using PharmacyIS.Domain.Enums;
using PharmacyIS.Domain.Reports;

namespace PharmacyIS.Data.Repositories;

/// <summary>
/// Заявки за справките на системата. Обобщенията се изчисляват от СУБД,
/// а не в приложението – така се пренася минимален обем данни по мрежата
/// и се използват изградените индекси.
/// </summary>
public class ReportRepository
{
    private readonly PharmacyDb _db;

    public ReportRepository(PharmacyDb db) => _db = db;

    private static (string Name, object? Value)[] PeriodParameters(DateTime from, DateTime to) =>
    [
        ("@from", from.Date),
        ("@to", to.Date.AddDays(1).AddSeconds(-1))
    ];

    /// <summary>Справка „Дневен оборот“ – обобщение по календарни дни.</summary>
    public List<DailyTurnoverRow> GetDailyTurnover(DateTime from, DateTime to) => _db.Query($"""
        SELECT
            DATE(s.SaleDate) AS SaleDay,
            COUNT(DISTINCT s.SaleId) AS SalesCount,
            COALESCE(SUM(si.Quantity), 0) AS ItemsSold,
            COALESCE(SUM(si.Quantity * si.UnitPrice), 0) AS Turnover,
            COALESCE(SUM(si.Quantity * si.UnitCost), 0) AS Cost
        FROM Sales s
        INNER JOIN SaleItems si ON si.SaleId = s.SaleId
        WHERE s.Status = {(int)DocumentStatus.Completed}
          AND s.SaleDate BETWEEN @from AND @to
        GROUP BY DATE(s.SaleDate)
        ORDER BY SaleDay DESC;
        """,
        (DbDataReader r) => new DailyTurnoverRow
        {
            Date = r.GetDateTime("SaleDay"),
            SalesCount = r.GetInt("SalesCount"),
            ItemsSold = r.GetInt("ItemsSold"),
            Turnover = r.GetDecimal("Turnover"),
            Cost = r.GetDecimal("Cost")
        },
        PeriodParameters(from, to));

    /// <summary>Справка „Най-продавани продукти“.</summary>
    public List<TopProductRow> GetTopProducts(DateTime from, DateTime to, int limit = 20) => _db.Query($"""
        SELECT
            m.Code AS MedicineCode,
            m.Name AS MedicineName,
            i.Name AS IngredientName,
            SUM(si.Quantity) AS QuantitySold,
            SUM(si.Quantity * si.UnitPrice) AS Turnover,
            SUM(si.Quantity * (si.UnitPrice - si.UnitCost)) AS Profit
        FROM SaleItems si
        INNER JOIN Sales s ON s.SaleId = si.SaleId
        INNER JOIN Medicines m ON m.MedicineId = si.MedicineId
        INNER JOIN ActiveIngredients i ON i.IngredientId = m.IngredientId
        WHERE s.Status = {(int)DocumentStatus.Completed}
          AND s.SaleDate BETWEEN @from AND @to
        GROUP BY m.MedicineId, m.Code, m.Name, i.Name
        ORDER BY QuantitySold DESC, Turnover DESC
        LIMIT {limit};
        """,
        (DbDataReader r) => new TopProductRow
        {
            MedicineCode = r.GetString("MedicineCode"),
            MedicineName = r.GetString("MedicineName"),
            IngredientName = r.GetString("IngredientName"),
            QuantitySold = r.GetInt("QuantitySold"),
            Turnover = r.GetDecimal("Turnover"),
            Profit = r.GetDecimal("Profit")
        },
        PeriodParameters(from, to));

    /// <summary>Справка „Продажби по служители“.</summary>
    public List<SalesByUserRow> GetSalesByUser(DateTime from, DateTime to) => _db.Query($"""
        SELECT
            u.FullName AS UserName,
            r.Name AS RoleName,
            COUNT(s.SaleId) AS SalesCount,
            COALESCE(SUM(s.TotalAmount), 0) AS Turnover
        FROM Sales s
        INNER JOIN Users u ON u.UserId = s.UserId
        INNER JOIN Roles r ON r.RoleId = u.RoleId
        WHERE s.Status = {(int)DocumentStatus.Completed}
          AND s.SaleDate BETWEEN @from AND @to
        GROUP BY u.UserId, u.FullName, r.Name
        ORDER BY Turnover DESC;
        """,
        (DbDataReader r) => new SalesByUserRow
        {
            UserName = r.GetString("UserName"),
            RoleName = r.GetString("RoleName"),
            SalesCount = r.GetInt("SalesCount"),
            Turnover = r.GetDecimal("Turnover")
        },
        PeriodParameters(from, to));

    /// <summary>Справка „Доставени количества по доставчици“.</summary>
    public List<DeliveriesBySupplierRow> GetDeliveriesBySupplier(DateTime from, DateTime to,
        int? supplierId = null)
    {
        var filter = supplierId is > 0 ? "AND d.SupplierId = @supplierId" : string.Empty;

        var parameters = new List<(string, object?)>(PeriodParameters(from, to));
        if (supplierId is > 0)
            parameters.Add(("@supplierId", supplierId.Value));

        return _db.Query($"""
            SELECT
                sup.Code AS SupplierCode,
                sup.Name AS SupplierName,
                m.Code AS MedicineCode,
                m.Name AS MedicineName,
                SUM(di.Quantity) AS Quantity,
                SUM(di.LineTotal) AS Amount
            FROM DeliveryItems di
            INNER JOIN Deliveries d ON d.DeliveryId = di.DeliveryId
            INNER JOIN Suppliers sup ON sup.SupplierId = d.SupplierId
            INNER JOIN Medicines m ON m.MedicineId = di.MedicineId
            WHERE d.Status = {(int)DocumentStatus.Completed}
              AND d.DeliveryDate BETWEEN @from AND @to {filter}
            GROUP BY sup.SupplierId, sup.Code, sup.Name, m.MedicineId, m.Code, m.Name
            ORDER BY sup.Name, m.Name;
            """,
            (DbDataReader r) => new DeliveriesBySupplierRow
            {
                SupplierCode = r.GetString("SupplierCode"),
                SupplierName = r.GetString("SupplierName"),
                MedicineCode = r.GetString("MedicineCode"),
                MedicineName = r.GetString("MedicineName"),
                Quantity = r.GetInt("Quantity"),
                Amount = r.GetDecimal("Amount")
            },
            parameters.ToArray());
    }

    /// <summary>
    /// Справка „Складова наличност“. Стойността на наличността се изчислява
    /// по доставни цени на конкретните партиди, а не по средна цена.
    /// </summary>
    public List<StockRow> GetStockReport(bool onlyBelowMinimum = false)
    {
        var having = onlyBelowMinimum ? "HAVING COALESCE(SUM(b.Quantity), 0) <= m.MinStock" : string.Empty;

        return _db.Query($"""
            SELECT
                m.Code AS MedicineCode,
                m.Name AS MedicineName,
                f.Name AS FormName,
                COALESCE(SUM(b.Quantity), 0) AS Quantity,
                m.MinStock,
                m.Price AS RetailPrice,
                COALESCE(SUM(b.Quantity * b.PurchasePrice), 0) AS StockValue,
                MIN(b.ExpiryDate) AS NearestExpiry
            FROM Medicines m
            INNER JOIN DosageForms f ON f.FormId = m.FormId
            LEFT JOIN Batches b ON b.MedicineId = m.MedicineId AND b.Quantity > 0
            WHERE m.IsActive = 1
            GROUP BY m.MedicineId, m.Code, m.Name, f.Name, m.MinStock, m.Price
            {having}
            ORDER BY m.Name;
            """,
            (DbDataReader r) => new StockRow
            {
                MedicineCode = r.GetString("MedicineCode"),
                MedicineName = r.GetString("MedicineName"),
                FormName = r.GetString("FormName"),
                Quantity = r.GetInt("Quantity"),
                MinStock = r.GetInt("MinStock"),
                RetailPrice = r.GetDecimal("RetailPrice"),
                StockValue = r.GetDecimal("StockValue"),
                NearestExpiry = r.GetNullableDateTime("NearestExpiry")
            });
    }

    /// <summary>Справка „Изтичащи срокове на годност“.</summary>
    public List<ExpiryRow> GetExpiryReport(DateTime asOf, int daysAhead) => _db.Query("""
        SELECT
            m.Code AS MedicineCode,
            m.Name AS MedicineName,
            b.BatchNumber,
            b.ExpiryDate,
            b.Quantity,
            b.Quantity * b.PurchasePrice AS Value
        FROM Batches b
        INNER JOIN Medicines m ON m.MedicineId = b.MedicineId
        WHERE b.Quantity > 0 AND b.ExpiryDate <= @limit
        ORDER BY b.ExpiryDate, m.Name;
        """,
        (DbDataReader r) =>
        {
            var expiry = r.GetDateTime("ExpiryDate");
            return new ExpiryRow
            {
                MedicineCode = r.GetString("MedicineCode"),
                MedicineName = r.GetString("MedicineName"),
                BatchNumber = r.GetString("BatchNumber"),
                ExpiryDate = expiry,
                Quantity = r.GetInt("Quantity"),
                DaysLeft = (expiry.Date - asOf.Date).Days,
                Value = r.GetDecimal("Value")
            };
        },
        ("@limit", asOf.Date.AddDays(daysAhead)));

    /// <summary>Справка „Бракувани количества“.</summary>
    public List<WriteOffRow> GetWriteOffReport(DateTime from, DateTime to) => _db.Query($"""
        SELECT
            w.WriteOffDate,
            w.DocNumber,
            w.Reason,
            m.Code AS MedicineCode,
            m.Name AS MedicineName,
            b.BatchNumber,
            wi.Quantity,
            wi.LineTotal AS Value
        FROM WriteOffItems wi
        INNER JOIN WriteOffs w ON w.WriteOffId = wi.WriteOffId
        INNER JOIN Medicines m ON m.MedicineId = wi.MedicineId
        INNER JOIN Batches b ON b.BatchId = wi.BatchId
        WHERE w.Status = {(int)DocumentStatus.Completed}
          AND w.WriteOffDate BETWEEN @from AND @to
        ORDER BY w.WriteOffDate DESC, m.Name;
        """,
        (DbDataReader r) => new WriteOffRow
        {
            Date = r.GetDateTime("WriteOffDate"),
            DocNumber = r.GetString("DocNumber"),
            MedicineCode = r.GetString("MedicineCode"),
            MedicineName = r.GetString("MedicineName"),
            BatchNumber = r.GetString("BatchNumber"),
            Quantity = r.GetInt("Quantity"),
            Value = r.GetDecimal("Value"),
            Reason = r.GetString("Reason")
        },
        PeriodParameters(from, to));
}
