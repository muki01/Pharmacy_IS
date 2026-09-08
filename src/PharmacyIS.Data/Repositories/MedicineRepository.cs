using System.Data.Common;
using PharmacyIS.Data.Infrastructure;
using PharmacyIS.Domain.Entities;

namespace PharmacyIS.Data.Repositories;

/// <summary>
/// Достъп до каталога на лекарствените продукти.
/// Всички заявки връщат и текущата наличност, изчислена от партидите,
/// за да може интерфейсът да я покаже без допълнително обръщение към базата.
/// </summary>
public class MedicineRepository
{
    private readonly PharmacyDb _db;

    public MedicineRepository(PharmacyDb db) => _db = db;

    private const string SelectSql = """
        SELECT
            m.MedicineId, m.Code, m.Name, m.IngredientId, m.FormId, m.Manufacturer,
            m.Strength, m.Price, m.RequiresPrescription, m.MinStock, m.Barcode, m.IsActive,
            i.Name AS IngredientName,
            f.Name AS FormName,
            COALESCE(s.StockQuantity, 0) AS StockQuantity,
            s.NearestExpiry
        FROM Medicines m
        INNER JOIN ActiveIngredients i ON i.IngredientId = m.IngredientId
        INNER JOIN DosageForms f ON f.FormId = m.FormId
        LEFT JOIN (
            SELECT b.MedicineId,
                   SUM(b.Quantity) AS StockQuantity,
                   MIN(b.ExpiryDate) AS NearestExpiry
            FROM Batches b
            WHERE b.Quantity > 0
            GROUP BY b.MedicineId
        ) s ON s.MedicineId = m.MedicineId
        """;

    public List<Medicine> GetAll(bool onlyActive = true)
    {
        var filter = onlyActive ? "WHERE m.IsActive = 1" : string.Empty;
        return _db.Query($"{SelectSql} {filter} ORDER BY m.Name;", Map);
    }

    public Medicine? GetById(int medicineId)
        => _db.QuerySingle($"{SelectSql} WHERE m.MedicineId = @id;", Map, ("@id", medicineId));

    public Medicine? GetByBarcode(string barcode)
        => _db.QuerySingle($"{SelectSql} WHERE m.Barcode = @barcode AND m.IsActive = 1;",
            Map, ("@barcode", barcode));

    /// <summary>
    /// Търсене по наименование, код, баркод, активна съставка или производител.
    /// Използва се от касовия екран за бързо намиране на продукт.
    /// </summary>
    public List<Medicine> Search(string term, int? ingredientId = null, int? formId = null,
        int? supplierId = null, bool onlyActive = true)
    {
        var conditions = new List<string>();
        var parameters = new List<(string, object?)>();

        if (onlyActive)
            conditions.Add("m.IsActive = 1");

        if (!string.IsNullOrWhiteSpace(term))
        {
            // Сравнението не зависи от регистъра на буквите и започва от
            // началото на дума, а не от произволно място в текста. При
            // въведено „ана“ се намира „Аналгин“, но не и „Панадол“.
            string Like(string expression, string parameter)
                => SqlDialect.Like(_db.Provider, expression, parameter);

            string FromWordStart(string expression) =>
                $"({Like(expression, "@start")}" +
                $" OR {Like(expression, "@word")}" +
                $" OR {Like(expression, "@dash")})";

            conditions.Add($"""
                ({FromWordStart("m.Name")}
                 OR {Like("m.Code", "@start")}
                 OR {Like("COALESCE(m.Barcode, '')", "@start")}
                 OR {FromWordStart("i.Name")}
                 OR {FromWordStart("COALESCE(m.Manufacturer, '')")})
                """);

            var text = SqlDialect.EscapeLike(term.Trim().ToLowerInvariant());
            parameters.Add(("@start", $"{text}%"));
            parameters.Add(("@word", $"% {text}%"));
            parameters.Add(("@dash", $"%-{text}%"));
        }

        if (ingredientId is > 0)
        {
            conditions.Add("m.IngredientId = @ingredientId");
            parameters.Add(("@ingredientId", ingredientId.Value));
        }

        if (formId is > 0)
        {
            conditions.Add("m.FormId = @formId");
            parameters.Add(("@formId", formId.Value));
        }

        // Търсене по доставчик – продукти, доставяни поне веднъж от него.
        if (supplierId is > 0)
        {
            conditions.Add("""
                EXISTS (
                    SELECT 1 FROM DeliveryItems di
                    INNER JOIN Deliveries d ON d.DeliveryId = di.DeliveryId
                    WHERE di.MedicineId = m.MedicineId AND d.SupplierId = @supplierId
                )
                """);
            parameters.Add(("@supplierId", supplierId.Value));
        }

        var where = conditions.Count > 0 ? "WHERE " + string.Join(" AND ", conditions) : string.Empty;

        return _db.Query($"{SelectSql} {where} ORDER BY m.Name;", Map, parameters.ToArray());
    }

    public bool CodeExists(string code, int excludeId = 0) => Convert.ToInt32(_db.ExecuteScalar(
        "SELECT COUNT(*) FROM Medicines WHERE Code = @code AND MedicineId <> @id;",
        ("@code", code), ("@id", excludeId))) > 0;

    public bool BarcodeExists(string barcode, int excludeId = 0) => Convert.ToInt32(_db.ExecuteScalar(
        "SELECT COUNT(*) FROM Medicines WHERE Barcode = @barcode AND MedicineId <> @id;",
        ("@barcode", barcode), ("@id", excludeId))) > 0;

    public int Insert(Medicine medicine)
    {
        _db.Execute("""
            INSERT INTO Medicines
                (Code, Name, IngredientId, FormId, Manufacturer, Strength,
                 Price, RequiresPrescription, MinStock, Barcode, IsActive)
            VALUES
                (@code, @name, @ingredientId, @formId, @manufacturer, @strength,
                 @price, @rx, @minStock, @barcode, @isActive);
            """,
            ("@code", medicine.Code),
            ("@name", medicine.Name),
            ("@ingredientId", medicine.IngredientId),
            ("@formId", medicine.FormId),
            ("@manufacturer", medicine.Manufacturer),
            ("@strength", medicine.Strength),
            ("@price", medicine.Price),
            ("@rx", medicine.RequiresPrescription),
            ("@minStock", medicine.MinStock),
            ("@barcode", string.IsNullOrWhiteSpace(medicine.Barcode) ? null : medicine.Barcode),
            ("@isActive", medicine.IsActive));

        return _db.LastInsertId();
    }

    public void Update(Medicine medicine)
    {
        _db.Execute("""
            UPDATE Medicines
            SET Code = @code, Name = @name, IngredientId = @ingredientId, FormId = @formId,
                Manufacturer = @manufacturer, Strength = @strength, Price = @price,
                RequiresPrescription = @rx, MinStock = @minStock, Barcode = @barcode,
                IsActive = @isActive
            WHERE MedicineId = @id;
            """,
            ("@code", medicine.Code),
            ("@name", medicine.Name),
            ("@ingredientId", medicine.IngredientId),
            ("@formId", medicine.FormId),
            ("@manufacturer", medicine.Manufacturer),
            ("@strength", medicine.Strength),
            ("@price", medicine.Price),
            ("@rx", medicine.RequiresPrescription),
            ("@minStock", medicine.MinStock),
            ("@barcode", string.IsNullOrWhiteSpace(medicine.Barcode) ? null : medicine.Barcode),
            ("@isActive", medicine.IsActive),
            ("@id", medicine.MedicineId));
    }

    /// <summary>Проверява дали продуктът участва в складови документи.</summary>
    public bool HasDocuments(int medicineId) => Convert.ToInt32(_db.ExecuteScalar(
        """
        SELECT
            (SELECT COUNT(*) FROM SaleItems     WHERE MedicineId = @id)
          + (SELECT COUNT(*) FROM DeliveryItems WHERE MedicineId = @id)
          + (SELECT COUNT(*) FROM Batches       WHERE MedicineId = @id);
        """, ("@id", medicineId))) > 0;

    /// <summary>Изтрива продукт. Допустимо е само за продукт без документи и партиди.</summary>
    public void Delete(int medicineId)
        => _db.Execute("DELETE FROM Medicines WHERE MedicineId = @id;", ("@id", medicineId));

    /// <summary>Деактивира продукт – спира продажбата му, но запазва историята.</summary>
    public void Deactivate(int medicineId)
        => _db.Execute("UPDATE Medicines SET IsActive = 0 WHERE MedicineId = @id;", ("@id", medicineId));

    private static Medicine Map(DbDataReader r) => new()
    {
        MedicineId = r.GetInt("MedicineId"),
        Code = r.GetString("Code"),
        Name = r.GetString("Name"),
        IngredientId = r.GetInt("IngredientId"),
        IngredientName = r.GetString("IngredientName"),
        FormId = r.GetInt("FormId"),
        FormName = r.GetString("FormName"),
        Manufacturer = r.GetNullableString("Manufacturer"),
        Strength = r.GetNullableString("Strength"),
        Price = r.GetDecimal("Price"),
        RequiresPrescription = r.GetBool("RequiresPrescription"),
        MinStock = r.GetInt("MinStock"),
        Barcode = r.GetNullableString("Barcode"),
        IsActive = r.GetBool("IsActive"),
        StockQuantity = r.GetInt("StockQuantity"),
        NearestExpiry = r.GetNullableDateTime("NearestExpiry")
    };
}
