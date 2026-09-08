using System.Data.Common;
using PharmacyIS.Data.Infrastructure;
using PharmacyIS.Domain.Entities;

namespace PharmacyIS.Data.Repositories;

/// <summary>
/// Достъп до номенклатурните файлове (лекарствени форми, активни съставки)
/// и до системните настройки.
/// </summary>
public class NomenclatureRepository
{
    private readonly PharmacyDb _db;

    public NomenclatureRepository(PharmacyDb db) => _db = db;

    // --- Лекарствени форми -------------------------------------------------

    public List<DosageForm> GetDosageForms() => _db.Query(
        "SELECT FormId, Code, Name FROM DosageForms ORDER BY Code;",
        r => new DosageForm
        {
            FormId = r.GetInt("FormId"),
            Code = r.GetString("Code"),
            Name = r.GetString("Name")
        });

    public int InsertDosageForm(DosageForm form)
    {
        _db.Execute("INSERT INTO DosageForms (Code, Name) VALUES (@code, @name);",
            ("@code", form.Code), ("@name", form.Name));
        return _db.LastInsertId();
    }

    public void UpdateDosageForm(DosageForm form)
    {
        _db.Execute("UPDATE DosageForms SET Code = @code, Name = @name WHERE FormId = @id;",
            ("@code", form.Code), ("@name", form.Name), ("@id", form.FormId));
    }

    // --- Активни съставки --------------------------------------------------

    public List<ActiveIngredient> GetActiveIngredients() => _db.Query(
        "SELECT IngredientId, Code, Name FROM ActiveIngredients ORDER BY Name;",
        r => new ActiveIngredient
        {
            IngredientId = r.GetInt("IngredientId"),
            Code = r.GetString("Code"),
            Name = r.GetString("Name")
        });

    public int InsertIngredient(ActiveIngredient ingredient)
    {
        _db.Execute("INSERT INTO ActiveIngredients (Code, Name) VALUES (@code, @name);",
            ("@code", ingredient.Code), ("@name", ingredient.Name));
        return _db.LastInsertId();
    }

    public void UpdateIngredient(ActiveIngredient ingredient)
    {
        _db.Execute("UPDATE ActiveIngredients SET Code = @code, Name = @name WHERE IngredientId = @id;",
            ("@code", ingredient.Code), ("@name", ingredient.Name), ("@id", ingredient.IngredientId));
    }

    /// <summary>Проверява дали номенклатурна позиция се използва от продукт.</summary>
    public bool IsIngredientUsed(int ingredientId) => Convert.ToInt32(_db.ExecuteScalar(
        "SELECT COUNT(*) FROM Medicines WHERE IngredientId = @id;", ("@id", ingredientId))) > 0;

    public bool IsFormUsed(int formId) => Convert.ToInt32(_db.ExecuteScalar(
        "SELECT COUNT(*) FROM Medicines WHERE FormId = @id;", ("@id", formId))) > 0;

    // --- Системни настройки ------------------------------------------------

    /// <summary>Връща стойността на настройка или подадената стойност по подразбиране.</summary>
    public string GetSetting(string key, string defaultValue)
    {
        var value = _db.ExecuteScalar(
            "SELECT SettingValue FROM Settings WHERE SettingKey = @key;", ("@key", key));

        return value is null or DBNull ? defaultValue : Convert.ToString(value) ?? defaultValue;
    }

    /// <summary>Връща целочислена настройка.</summary>
    public int GetIntSetting(string key, int defaultValue)
        => int.TryParse(GetSetting(key, defaultValue.ToString()), out var result) ? result : defaultValue;

    /// <summary>Записва стойност на настройка, като я създава при нужда.</summary>
    public void SetSetting(string key, string value)
    {
        var affected = _db.Execute(
            "UPDATE Settings SET SettingValue = @value WHERE SettingKey = @key;",
            ("@key", key), ("@value", value));

        if (affected == 0)
        {
            _db.Execute("INSERT INTO Settings (SettingKey, SettingValue) VALUES (@key, @value);",
                ("@key", key), ("@value", value));
        }
    }

    public List<(string Key, string Value, string? Description)> GetAllSettings() => _db.Query(
        "SELECT SettingKey, SettingValue, Description FROM Settings ORDER BY SettingKey;",
        (DbDataReader r) => (
            r.GetString("SettingKey"),
            r.GetString("SettingValue"),
            r.GetNullableString("Description")));
}
