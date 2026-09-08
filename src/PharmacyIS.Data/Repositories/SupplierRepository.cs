using System.Data.Common;
using PharmacyIS.Data.Infrastructure;
using PharmacyIS.Domain.Entities;

namespace PharmacyIS.Data.Repositories;

/// <summary>Достъп до данните за доставчиците.</summary>
public class SupplierRepository
{
    private readonly PharmacyDb _db;

    public SupplierRepository(PharmacyDb db) => _db = db;

    private const string SelectSql = """
        SELECT SupplierId, Code, Name, Bulstat, ContactPerson, Phone, Email, Address, IsActive
        FROM Suppliers
        """;

    /// <summary>Връща доставчиците, по избор само активните.</summary>
    public List<Supplier> GetAll(bool onlyActive = false)
    {
        var filter = onlyActive ? "WHERE IsActive = 1" : string.Empty;
        return _db.Query($"{SelectSql} {filter} ORDER BY Name;", Map);
    }

    public Supplier? GetById(int supplierId)
        => _db.QuerySingle($"{SelectSql} WHERE SupplierId = @id;", Map, ("@id", supplierId));

    /// <summary>
    /// Търси доставчици по част от наименованието, кода или ЕИК.
    /// Стойността се подава като параметър, не се вгражда в текста на заявката.
    /// </summary>
    public List<Supplier> Search(string term)
    {
        if (string.IsNullOrWhiteSpace(term))
            return GetAll();

        // Съвпадението започва от началото на дума и не зависи от регистъра
        // на буквите – вж. SqlDialect.Like.
        string Like(string expression, string parameter)
            => SqlDialect.Like(_db.Provider, expression, parameter);

        var text = SqlDialect.EscapeLike(term.Trim().ToLowerInvariant());

        return _db.Query($"""
            {SelectSql}
            WHERE {Like("Name", "@start")}
               OR {Like("Name", "@word")}
               OR {Like("Name", "@dash")}
               OR {Like("Code", "@start")}
               OR {Like("COALESCE(Bulstat, '')", "@start")}
            ORDER BY Name;
            """, Map,
            ("@start", $"{text}%"),
            ("@word", $"% {text}%"),
            ("@dash", $"%-{text}%"));
    }

    public bool CodeExists(string code, int excludeId = 0) => Convert.ToInt32(_db.ExecuteScalar(
        "SELECT COUNT(*) FROM Suppliers WHERE Code = @code AND SupplierId <> @id;",
        ("@code", code), ("@id", excludeId))) > 0;

    public int Insert(Supplier supplier)
    {
        _db.Execute("""
            INSERT INTO Suppliers (Code, Name, Bulstat, ContactPerson, Phone, Email, Address, IsActive)
            VALUES (@code, @name, @bulstat, @contact, @phone, @email, @address, @isActive);
            """,
            ("@code", supplier.Code),
            ("@name", supplier.Name),
            ("@bulstat", supplier.Bulstat),
            ("@contact", supplier.ContactPerson),
            ("@phone", supplier.Phone),
            ("@email", supplier.Email),
            ("@address", supplier.Address),
            ("@isActive", supplier.IsActive));

        return _db.LastInsertId();
    }

    public void Update(Supplier supplier)
    {
        _db.Execute("""
            UPDATE Suppliers
            SET Code = @code, Name = @name, Bulstat = @bulstat, ContactPerson = @contact,
                Phone = @phone, Email = @email, Address = @address, IsActive = @isActive
            WHERE SupplierId = @id;
            """,
            ("@code", supplier.Code),
            ("@name", supplier.Name),
            ("@bulstat", supplier.Bulstat),
            ("@contact", supplier.ContactPerson),
            ("@phone", supplier.Phone),
            ("@email", supplier.Email),
            ("@address", supplier.Address),
            ("@isActive", supplier.IsActive),
            ("@id", supplier.SupplierId));
    }

    /// <summary>Проверява дали за доставчика има регистрирани доставки.</summary>
    public bool HasDeliveries(int supplierId) => Convert.ToInt32(_db.ExecuteScalar(
        "SELECT COUNT(*) FROM Deliveries WHERE SupplierId = @id;", ("@id", supplierId))) > 0;

    /// <summary>
    /// Изтрива доставчик. Прилага се само за доставчици без доставки –
    /// в останалите случаи записът се деактивира, за да се запази историята.
    /// </summary>
    public void Delete(int supplierId)
        => _db.Execute("DELETE FROM Suppliers WHERE SupplierId = @id;", ("@id", supplierId));

    private static Supplier Map(DbDataReader r) => new()
    {
        SupplierId = r.GetInt("SupplierId"),
        Code = r.GetString("Code"),
        Name = r.GetString("Name"),
        Bulstat = r.GetNullableString("Bulstat"),
        ContactPerson = r.GetNullableString("ContactPerson"),
        Phone = r.GetNullableString("Phone"),
        Email = r.GetNullableString("Email"),
        Address = r.GetNullableString("Address"),
        IsActive = r.GetBool("IsActive")
    };
}
