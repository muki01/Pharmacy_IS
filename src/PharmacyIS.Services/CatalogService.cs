using PharmacyIS.Data.Infrastructure;
using PharmacyIS.Data.Repositories;
using PharmacyIS.Domain.Entities;
using PharmacyIS.Domain.Exceptions;

namespace PharmacyIS.Services;

/// <summary>
/// Поддържане на каталога на лекарствените продукти, на доставчиците
/// и на номенклатурните файлове.
///
/// Четенето и търсенето са достъпни за всички служители, тъй като са
/// необходими при ежедневната работа на гишето. Промяната на каталога
/// обаче засяга ценообразуването и асортимента и затова е запазена за
/// потребители с роля „Управител“.
/// </summary>
public class CatalogService
{
    private readonly DbSessionFactory _sessions;

    public CatalogService(DbSessionFactory sessions) => _sessions = sessions;

    // ==================================================================
    //  Лекарствени продукти
    // ==================================================================

    public List<Medicine> GetMedicines(bool onlyActive = true)
    {
        using var db = _sessions.Create();
        return new MedicineRepository(db).GetAll(onlyActive);
    }

    public Medicine? GetMedicine(int medicineId)
    {
        using var db = _sessions.Create();
        return new MedicineRepository(db).GetById(medicineId);
    }

    public Medicine? GetMedicineByBarcode(string barcode)
    {
        if (string.IsNullOrWhiteSpace(barcode)) return null;

        using var db = _sessions.Create();
        return new MedicineRepository(db).GetByBarcode(barcode.Trim());
    }

    /// <summary>
    /// Търсене на лекарства по свободен текст и по номенклатурни критерии.
    /// </summary>
    public List<Medicine> SearchMedicines(string term, int? ingredientId = null,
        int? formId = null, int? supplierId = null, bool onlyActive = true)
    {
        using var db = _sessions.Create();
        return new MedicineRepository(db).Search(term, ingredientId, formId, supplierId, onlyActive);
    }

    /// <summary>Добавя нов продукт в каталога. Достъпно само за управител.</summary>
    public int AddMedicine(User currentUser, Medicine medicine)
    {
        AuthService.RequireManager(currentUser, "Добавяне на лекарствен продукт");
        ValidateMedicine(medicine);

        using var db = _sessions.Create();
        var repo = new MedicineRepository(db);

        if (repo.CodeExists(medicine.Code))
            throw new ValidationException($"Код „{medicine.Code}“ вече се използва от друг продукт.");

        if (!string.IsNullOrWhiteSpace(medicine.Barcode) && repo.BarcodeExists(medicine.Barcode))
            throw new ValidationException($"Баркод „{medicine.Barcode}“ вече се използва от друг продукт.");

        return repo.Insert(medicine);
    }

    /// <summary>Обновява данните на продукт. Достъпно само за управител.</summary>
    public void UpdateMedicine(User currentUser, Medicine medicine)
    {
        AuthService.RequireManager(currentUser, "Редактиране на лекарствен продукт");
        ValidateMedicine(medicine);

        using var db = _sessions.Create();
        var repo = new MedicineRepository(db);

        if (repo.CodeExists(medicine.Code, medicine.MedicineId))
            throw new ValidationException($"Код „{medicine.Code}“ вече се използва от друг продукт.");

        if (!string.IsNullOrWhiteSpace(medicine.Barcode) &&
            repo.BarcodeExists(medicine.Barcode, medicine.MedicineId))
        {
            throw new ValidationException($"Баркод „{medicine.Barcode}“ вече се използва от друг продукт.");
        }

        repo.Update(medicine);
    }

    /// <summary>
    /// Премахва продукт от каталога. Ако за продукта има складови документи,
    /// записът не се изтрива, а само се деактивира, за да се запази историята.
    /// </summary>
    /// <returns><c>true</c>, ако записът е изтрит; <c>false</c>, ако е само деактивиран.</returns>
    public bool RemoveMedicine(User currentUser, int medicineId)
    {
        AuthService.RequireManager(currentUser, "Премахване на лекарствен продукт");

        using var db = _sessions.Create();
        var repo = new MedicineRepository(db);

        if (repo.HasDocuments(medicineId))
        {
            repo.Deactivate(medicineId);
            return false;
        }

        repo.Delete(medicineId);
        return true;
    }

    // ==================================================================
    //  Доставчици
    // ==================================================================

    public List<Supplier> GetSuppliers(bool onlyActive = false)
    {
        using var db = _sessions.Create();
        return new SupplierRepository(db).GetAll(onlyActive);
    }

    public List<Supplier> SearchSuppliers(string term)
    {
        using var db = _sessions.Create();
        return new SupplierRepository(db).Search(term);
    }

    public int AddSupplier(User currentUser, Supplier supplier)
    {
        AuthService.RequireManager(currentUser, "Добавяне на доставчик");
        ValidateSupplier(supplier);

        using var db = _sessions.Create();
        var repo = new SupplierRepository(db);

        if (repo.CodeExists(supplier.Code))
            throw new ValidationException($"Код „{supplier.Code}“ вече се използва от друг доставчик.");

        return repo.Insert(supplier);
    }

    public void UpdateSupplier(User currentUser, Supplier supplier)
    {
        AuthService.RequireManager(currentUser, "Редактиране на доставчик");
        ValidateSupplier(supplier);

        using var db = _sessions.Create();
        var repo = new SupplierRepository(db);

        if (repo.CodeExists(supplier.Code, supplier.SupplierId))
            throw new ValidationException($"Код „{supplier.Code}“ вече се използва от друг доставчик.");

        repo.Update(supplier);
    }

    /// <summary>
    /// Премахва доставчик. При наличие на доставки записът се деактивира
    /// вместо да се изтрие.
    /// </summary>
    /// <returns><c>true</c>, ако записът е изтрит; <c>false</c>, ако е само деактивиран.</returns>
    public bool RemoveSupplier(User currentUser, int supplierId)
    {
        AuthService.RequireManager(currentUser, "Премахване на доставчик");

        using var db = _sessions.Create();
        var repo = new SupplierRepository(db);

        var supplier = repo.GetById(supplierId)
            ?? throw new DomainException("Доставчикът не е намерен.");

        if (repo.HasDeliveries(supplierId))
        {
            supplier.IsActive = false;
            repo.Update(supplier);
            return false;
        }

        repo.Delete(supplierId);
        return true;
    }

    // ==================================================================
    //  Номенклатури
    // ==================================================================

    public List<DosageForm> GetDosageForms()
    {
        using var db = _sessions.Create();
        return new NomenclatureRepository(db).GetDosageForms();
    }

    public List<ActiveIngredient> GetActiveIngredients()
    {
        using var db = _sessions.Create();
        return new NomenclatureRepository(db).GetActiveIngredients();
    }

    public int AddDosageForm(User currentUser, DosageForm form)
    {
        AuthService.RequireManager(currentUser, "Добавяне на лекарствена форма");
        ValidateNomenclature(form.Code, form.Name, "лекарствената форма");

        using var db = _sessions.Create();
        return new NomenclatureRepository(db).InsertDosageForm(form);
    }

    public void UpdateDosageForm(User currentUser, DosageForm form)
    {
        AuthService.RequireManager(currentUser, "Редактиране на лекарствена форма");
        ValidateNomenclature(form.Code, form.Name, "лекарствената форма");

        using var db = _sessions.Create();
        new NomenclatureRepository(db).UpdateDosageForm(form);
    }

    public int AddIngredient(User currentUser, ActiveIngredient ingredient)
    {
        AuthService.RequireManager(currentUser, "Добавяне на активна съставка");
        ValidateNomenclature(ingredient.Code, ingredient.Name, "активната съставка");

        using var db = _sessions.Create();
        return new NomenclatureRepository(db).InsertIngredient(ingredient);
    }

    public void UpdateIngredient(User currentUser, ActiveIngredient ingredient)
    {
        AuthService.RequireManager(currentUser, "Редактиране на активна съставка");
        ValidateNomenclature(ingredient.Code, ingredient.Name, "активната съставка");

        using var db = _sessions.Create();
        new NomenclatureRepository(db).UpdateIngredient(ingredient);
    }

    // ==================================================================
    //  Проверки на входните данни
    // ==================================================================

    private static void ValidateMedicine(Medicine medicine)
    {
        ArgumentNullException.ThrowIfNull(medicine);

        if (string.IsNullOrWhiteSpace(medicine.Code))
            throw new ValidationException("Въведете код на продукта.");

        if (!medicine.Code.All(char.IsDigit) || medicine.Code.Length != 8)
            throw new ValidationException("Кодът на продукта трябва да съдържа точно 8 цифри.");

        if (string.IsNullOrWhiteSpace(medicine.Name))
            throw new ValidationException("Въведете наименование на продукта.");

        if (medicine.IngredientId <= 0)
            throw new ValidationException("Изберете активна съставка.");

        if (medicine.FormId <= 0)
            throw new ValidationException("Изберете лекарствена форма.");

        // Нулева продажна цена означава отпускане без заплащане и почти
        // винаги е грешка при въвеждането, затова не се приема.
        if (medicine.Price <= 0)
            throw new ValidationException("Въведете продажна цена, по-голяма от нула.");

        if (medicine.Price > 100_000)
            throw new ValidationException("Въведената цена е извън допустимите граници.");

        if (medicine.MinStock < 0)
            throw new ValidationException("Минималната наличност не може да бъде отрицателна.");

        if (!string.IsNullOrWhiteSpace(medicine.Barcode) &&
            (medicine.Barcode.Length is < 8 or > 14 || !medicine.Barcode.All(char.IsDigit)))
        {
            throw new ValidationException("Баркодът трябва да съдържа между 8 и 14 цифри.");
        }

        medicine.Code = medicine.Code.Trim();
        medicine.Name = medicine.Name.Trim();
        medicine.Barcode = medicine.Barcode?.Trim();
    }

    private static void ValidateSupplier(Supplier supplier)
    {
        ArgumentNullException.ThrowIfNull(supplier);

        if (string.IsNullOrWhiteSpace(supplier.Code))
            throw new ValidationException("Въведете код на доставчика.");

        if (!supplier.Code.All(char.IsDigit) || supplier.Code.Length != 4)
            throw new ValidationException("Кодът на доставчика трябва да съдържа точно 4 цифри.");

        if (string.IsNullOrWhiteSpace(supplier.Name))
            throw new ValidationException("Въведете наименование на доставчика.");

        if (!string.IsNullOrWhiteSpace(supplier.Bulstat) &&
            (!supplier.Bulstat.All(char.IsDigit) || supplier.Bulstat.Length is not (9 or 13)))
        {
            throw new ValidationException("ЕИК/БУЛСТАТ трябва да съдържа 9 или 13 цифри.");
        }

        if (!string.IsNullOrWhiteSpace(supplier.Email) &&
            (!supplier.Email.Contains('@') || supplier.Email.EndsWith('@')))
        {
            throw new ValidationException("Въведеният адрес на електронна поща не е валиден.");
        }

        supplier.Code = supplier.Code.Trim();
        supplier.Name = supplier.Name.Trim();
    }

    private static void ValidateNomenclature(string code, string name, string what)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new ValidationException($"Въведете код на {what}.");

        if (string.IsNullOrWhiteSpace(name))
            throw new ValidationException($"Въведете наименование на {what}.");
    }
}
