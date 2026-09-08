using PharmacyIS.Data.Infrastructure;
using PharmacyIS.Domain.Entities;
using PharmacyIS.Domain.Enums;
using PharmacyIS.Domain.Exceptions;
using PharmacyIS.Services.Models;
using Xunit;

namespace PharmacyIS.Tests;

/// <summary>Тестове на удостоверяването, правата за достъп и каталога.</summary>
public class SecurityAndCatalogTests
{
    [Fact(DisplayName = "Отпечатъкът на паролата се различава при еднакви пароли")]
    public void PasswordHasher_ProducesDifferentHashesForSamePassword()
    {
        var saltA = PasswordHasher.CreateSalt();
        var saltB = PasswordHasher.CreateSalt();

        var hashA = PasswordHasher.ComputeHash("Parola123", saltA);
        var hashB = PasswordHasher.ComputeHash("Parola123", saltB);

        Assert.NotEqual(hashA, hashB);
        Assert.True(PasswordHasher.Verify("Parola123", saltA, hashA));
        Assert.True(PasswordHasher.Verify("Parola123", saltB, hashB));
    }

    [Fact(DisplayName = "Грешна парола не преминава проверката")]
    public void PasswordHasher_RejectsWrongPassword()
    {
        var salt = PasswordHasher.CreateSalt();
        var hash = PasswordHasher.ComputeHash("Parola123", salt);

        Assert.False(PasswordHasher.Verify("parola123", salt, hash));
        Assert.False(PasswordHasher.Verify("", salt, hash));
    }

    [Fact(DisplayName = "Успешен вход с коректни данни")]
    public void Login_SucceedsWithValidCredentials()
    {
        using var test = new TestDatabase();

        var user = test.Auth.Login("manager", "manager123");

        Assert.Equal(UserRole.Manager, user.Role);
        Assert.Equal("Управител", user.RoleName);
    }

    [Fact(DisplayName = "Вход с грешна парола се отказва")]
    public void Login_FailsWithWrongPassword()
    {
        using var test = new TestDatabase();

        Assert.Throws<DomainException>(() => test.Auth.Login("manager", "grehska"));
    }

    [Fact(DisplayName = "Профилът се блокира след пет неуспешни опита")]
    public void Login_BlocksAfterTooManyFailedAttempts()
    {
        using var test = new TestDatabase();

        for (var i = 0; i < AuthService_MaxAttempts; i++)
            Assert.Throws<DomainException>(() => test.Auth.Login("manager", "grehska"));

        // След изчерпване на опитите дори вярната парола не се приема.
        var error = Assert.Throws<DomainException>(() => test.Auth.Login("manager", "manager123"));
        Assert.Contains("блокиран", error.Message);
    }

    private const int AuthService_MaxAttempts = Services.AuthService.MaxFailedAttempts;

    [Fact(DisplayName = "Опит за влизане чрез SQL инжекция се отказва")]
    public void Login_IsNotVulnerableToSqlInjection()
    {
        using var test = new TestDatabase();

        // Класически опит за заобикаляне на проверката. Тъй като стойността
        // се предава като параметър, тя се третира като обикновен текст.
        Assert.Throws<DomainException>(() =>
            test.Auth.Login("manager' OR '1'='1", "cheatcode"));

        // Опит за унищожаване на таблица чрез входните данни.
        Assert.Throws<DomainException>(() =>
            test.Auth.Login("x'; DROP TABLE Users; --", "cheatcode"));

        // Таблицата съществува и потребителите са налични.
        Assert.NotEmpty(test.Auth.Login("manager", "manager123").Username);
    }

    [Fact(DisplayName = "Фармацевт няма достъп до финансовите справки")]
    public void Reports_RequireManagerRole()
    {
        using var test = new TestDatabase();
        var pharmacist = test.Auth.Login("farmacevt", "farmacevt123");

        Assert.Throws<AccessDeniedException>(() =>
            test.Reports.GetDailyTurnover(pharmacist, DateTime.Today.AddDays(-7), DateTime.Today));

        Assert.Throws<AccessDeniedException>(() =>
            test.Reports.GetTopProducts(pharmacist, DateTime.Today.AddDays(-7), DateTime.Today));
    }

    [Fact(DisplayName = "Оперативните справки са достъпни и за фармацевт")]
    public void OperationalReports_AreAvailableToPharmacist()
    {
        using var test = new TestDatabase();
        var pharmacist = test.Pharmacist;

        var stock = test.Reports.GetStockReport(pharmacist);
        var expiry = test.Reports.GetExpiryReport(pharmacist, daysAhead: 90);

        Assert.NotEmpty(stock);
        Assert.NotNull(expiry);
    }

    [Fact(DisplayName = "Оперативните справки не показват стойности на фармацевта")]
    public void OperationalReports_HideValuesFromPharmacist()
    {
        using var test = new TestDatabase();
        Seed(test);

        var forManager = test.Reports.GetStockReport(test.Manager);
        var forPharmacist = test.Reports.GetStockReport(test.Pharmacist);

        // Количествата съвпадат – ограничава се само стойностният показател.
        Assert.Equal(forManager.Sum(r => r.Quantity), forPharmacist.Sum(r => r.Quantity));
        Assert.True(forManager.Sum(r => r.StockValue) > 0m);
        Assert.All(forPharmacist, row => Assert.Equal(0m, row.StockValue));

        Assert.All(test.Reports.GetExpiryReport(test.Pharmacist, daysAhead: 3650),
            row => Assert.Equal(0m, row.Value));
    }

    [Fact(DisplayName = "Фармацевтът не вижда доставните цени по партидите")]
    public void Batches_HidePurchasePriceFromPharmacist()
    {
        using var test = new TestDatabase();
        Seed(test);

        var forManager = test.Stock.GetAvailableBatches(test.Manager);
        var forPharmacist = test.Stock.GetAvailableBatches(test.Pharmacist);

        Assert.Equal(forManager.Count, forPharmacist.Count);
        Assert.True(forManager.Sum(b => b.PurchasePrice) > 0m);
        Assert.All(forPharmacist, batch => Assert.Equal(0m, batch.PurchasePrice));
    }

    [Fact(DisplayName = "Регистърът на доставките е достъпен само за управител")]
    public void DeliveryJournal_RequiresManagerRole()
    {
        using var test = new TestDatabase();
        var from = DateTime.Today.AddMonths(-6);

        Assert.Throws<AccessDeniedException>(() =>
            test.Deliveries.GetDeliveries(test.Pharmacist, from, DateTime.Today));

        Assert.NotNull(test.Deliveries.GetDeliveries(test.Manager, from, DateTime.Today));
    }

    [Fact(DisplayName = "Фармацевтът може да заприхождава доставка")]
    public void RegisterDelivery_IsAllowedForPharmacist()
    {
        using var test = new TestDatabase();
        var medicine = test.FirstMedicine();
        var before = medicine.StockQuantity;

        var delivery = test.Deliveries.RegisterDelivery(new DeliveryRequest
        {
            SupplierId = test.FirstSupplier().SupplierId,
            UserId = test.PharmacistId,
            InvoiceNumber = "Ф-9001",
            Lines =
            {
                new DeliveryRequestLine
                {
                    MedicineId = medicine.MedicineId,
                    BatchNumber = "PH-0001",
                    ExpiryDate = DateTime.Today.AddYears(2),
                    Quantity = 12,
                    UnitPrice = 2.20m
                }
            }
        });

        Assert.NotEqual(0, delivery.DeliveryId);
        Assert.Equal(before + 12, test.Catalog.GetMedicine(medicine.MedicineId)!.StockQuantity);
    }

    [Fact(DisplayName = "Фармацевтът не може да променя каталога")]
    public void CatalogChanges_RequireManagerRole()
    {
        using var test = new TestDatabase();
        var pharmacist = test.Pharmacist;
        var existing = test.FirstMedicine();

        var medicine = new Medicine
        {
            Code = "98765432",
            Name = "Нов продукт",
            IngredientId = existing.IngredientId,
            FormId = existing.FormId,
            Price = 4.50m
        };

        Assert.Throws<AccessDeniedException>(() => test.Catalog.AddMedicine(pharmacist, medicine));
        Assert.Throws<AccessDeniedException>(() => test.Catalog.UpdateMedicine(pharmacist, existing));
        Assert.Throws<AccessDeniedException>(() =>
            test.Catalog.RemoveMedicine(pharmacist, existing.MedicineId));

        Assert.Throws<AccessDeniedException>(() => test.Catalog.AddSupplier(pharmacist,
            new Supplier { Code = "9998", Name = "Нов доставчик" }));

        Assert.Throws<AccessDeniedException>(() => test.Catalog.AddIngredient(pharmacist,
            new ActiveIngredient { Code = "ZZZ", Name = "Нова съставка" }));

        // Продуктът не е добавен въпреки опита.
        Assert.DoesNotContain(test.Catalog.GetMedicines(onlyActive: false),
            m => m.Code == "98765432");
    }

    [Fact(DisplayName = "Слаба парола не се приема")]
    public void CreateUser_RejectsWeakPassword()
    {
        using var test = new TestDatabase();
        var manager = test.Auth.Login("manager", "manager123");

        var newUser = new User
        {
            Username = "nov",
            FullName = "Нов Служител",
            Role = UserRole.Pharmacist
        };

        Assert.Throws<ValidationException>(() => test.Auth.CreateUser(manager, newUser, "kratka"));
        Assert.Throws<ValidationException>(() => test.Auth.CreateUser(manager, newUser, "samobukvi"));
    }

    [Fact(DisplayName = "Не може да се създаде втори профил със същото име")]
    public void CreateUser_RejectsDuplicateUsername()
    {
        using var test = new TestDatabase();
        var manager = test.Auth.Login("manager", "manager123");

        var duplicate = new User
        {
            Username = "farmacevt",
            FullName = "Друг Служител",
            Role = UserRole.Pharmacist
        };

        Assert.Throws<ValidationException>(() => test.Auth.CreateUser(manager, duplicate, "Parola123"));
    }

    [Fact(DisplayName = "Системата не остава без активен управител")]
    public void UpdateUser_KeepsAtLeastOneManager()
    {
        using var test = new TestDatabase();
        var manager = test.Auth.Login("manager", "manager123");

        // Управителят се опитва да понижи собствената си роля, а той е
        // единственият в системата – операцията трябва да бъде отказана.
        var edited = new User
        {
            UserId = manager.UserId,
            Username = manager.Username,
            FullName = manager.FullName,
            Role = UserRole.Pharmacist,
            IsActive = true
        };

        Assert.Throws<ValidationException>(() => test.Auth.UpdateUser(manager, edited));

        // Същото важи и при опит профилът да бъде деактивиран.
        edited.Role = UserRole.Manager;
        edited.IsActive = false;

        Assert.Throws<ValidationException>(() => test.Auth.UpdateUser(manager, edited));
    }

    [Fact(DisplayName = "Кодът на продукта трябва да съдържа осем цифри")]
    public void AddMedicine_ValidatesCodeFormat()
    {
        using var test = new TestDatabase();

        var medicine = new Medicine
        {
            Code = "123",
            Name = "Тестов продукт",
            IngredientId = test.Catalog.GetActiveIngredients()[0].IngredientId,
            FormId = test.Catalog.GetDosageForms()[0].FormId,
            Price = 5m
        };

        Assert.Throws<ValidationException>(() => test.Catalog.AddMedicine(test.Manager, medicine));
    }

    [Fact(DisplayName = "Отрицателна цена не се приема")]
    public void AddMedicine_RejectsNegativePrice()
    {
        using var test = new TestDatabase();

        var medicine = new Medicine
        {
            Code = "99999999",
            Name = "Тестов продукт",
            IngredientId = test.Catalog.GetActiveIngredients()[0].IngredientId,
            FormId = test.Catalog.GetDosageForms()[0].FormId,
            Price = -1m
        };

        Assert.Throws<ValidationException>(() => test.Catalog.AddMedicine(test.Manager, medicine));
    }

    [Fact(DisplayName = "Не се допускат два продукта с еднакъв код")]
    public void AddMedicine_RejectsDuplicateCode()
    {
        using var test = new TestDatabase();
        var existing = test.FirstMedicine();

        var medicine = new Medicine
        {
            Code = existing.Code,
            Name = "Друг продукт",
            IngredientId = existing.IngredientId,
            FormId = existing.FormId,
            Price = 5m
        };

        Assert.Throws<ValidationException>(() => test.Catalog.AddMedicine(test.Manager, medicine));
    }

    [Fact(DisplayName = "Търсенето намира продукт по активна съставка")]
    public void SearchMedicines_FindsByActiveIngredient()
    {
        using var test = new TestDatabase();

        var results = test.Catalog.SearchMedicines("Парацетамол");

        Assert.NotEmpty(results);
        Assert.All(results, m =>
            Assert.True(m.IngredientName.Contains("Парацетамол", StringComparison.OrdinalIgnoreCase)
                        || m.Name.Contains("Парацетамол", StringComparison.OrdinalIgnoreCase)));
    }

    [Fact(DisplayName = "Търсенето на кирилица не зависи от регистъра на буквите")]
    public void SearchMedicines_IsCaseInsensitiveForCyrillic()
    {
        using var test = new TestDatabase();

        var lower = test.Catalog.SearchMedicines("аналгин");
        var upper = test.Catalog.SearchMedicines("АНАЛГИН");
        var mixed = test.Catalog.SearchMedicines("АнАлГиН");

        Assert.NotEmpty(lower);
        Assert.Equal(lower.Count, upper.Count);
        Assert.Equal(lower.Count, mixed.Count);

        // Същото важи и за търсенето по активна съставка и по производител.
        Assert.NotEmpty(test.Catalog.SearchMedicines("парацетамол"));
        Assert.NotEmpty(test.Catalog.SearchMedicines("софарма"));
    }

    [Fact(DisplayName = "Търсенето на доставчик не зависи от регистъра")]
    public void SearchSuppliers_IsCaseInsensitive()
    {
        using var test = new TestDatabase();
        var supplier = test.FirstSupplier();

        var asStored = test.Catalog.SearchSuppliers(supplier.Name);
        var lowered = test.Catalog.SearchSuppliers(supplier.Name.ToLowerInvariant());
        var uppered = test.Catalog.SearchSuppliers(supplier.Name.ToUpperInvariant());

        Assert.NotEmpty(asStored);
        Assert.Equal(asStored.Count, lowered.Count);
        Assert.Equal(asStored.Count, uppered.Count);
    }

    [Fact(DisplayName = "Потребителското име при вход не зависи от регистъра")]
    public void Login_IsCaseInsensitiveForUsername()
    {
        using var test = new TestDatabase();

        Assert.Equal("manager", test.Auth.Login("MANAGER", "manager123").Username);
        Assert.Equal("manager", test.Auth.Login("Manager", "manager123").Username);
        Assert.Equal("manager", test.Auth.Login("  manager  ", "manager123").Username);

        // Паролата остава чувствителна към регистъра.
        Assert.Throws<DomainException>(() => test.Auth.Login("manager", "MANAGER123"));
    }

    [Fact(DisplayName = "Търсенето съвпада от началото на дума, а не по средата")]
    public void SearchMedicines_MatchesFromWordStart()
    {
        using var test = new TestDatabase();

        var result = test.Catalog.SearchMedicines("ана");

        // „Аналгин“ започва с въведеното, „Панадол“ го съдържа по средата.
        Assert.Contains(result, m => m.Name.StartsWith("Аналгин", StringComparison.Ordinal));
        Assert.DoesNotContain(result, m => m.Name.Contains("Панадол", StringComparison.Ordinal));
    }

    [Fact(DisplayName = "Търсенето намира и по втора дума от наименованието")]
    public void SearchMedicines_MatchesAnyWordInName()
    {
        using var test = new TestDatabase();

        // „сироп“ е втора дума в „Парацетамол сироп за деца“.
        var bySecondWord = test.Catalog.SearchMedicines("сироп");
        Assert.Contains(bySecondWord, m => m.Name.Contains("сироп", StringComparison.Ordinal));

        // „Шпа“ следва тире в „Но-Шпа 40 mg“.
        var afterDash = test.Catalog.SearchMedicines("шпа");
        Assert.Contains(afterDash, m => m.Name.Contains("Шпа", StringComparison.Ordinal));
    }

    [Fact(DisplayName = "Заместващите знаци в търсенето се приемат буквално")]
    public void SearchMedicines_EscapesWildcards()
    {
        using var test = new TestDatabase();

        // Без екраниране „%“ би върнало целия каталог.
        Assert.Empty(test.Catalog.SearchMedicines("%"));
        Assert.Empty(test.Catalog.SearchMedicines("_"));
        Assert.NotEmpty(test.Catalog.GetMedicines());
    }

    [Fact(DisplayName = "Търсенето по несъществуващ текст връща празен резултат")]
    public void SearchMedicines_ReturnsEmptyForUnknownTerm()
    {
        using var test = new TestDatabase();

        Assert.Empty(test.Catalog.SearchMedicines("НямаТакъвПродукт2026"));
    }

    [Fact(DisplayName = "Специални знаци в търсенето не нарушават заявката")]
    public void SearchMedicines_HandlesSpecialCharacters()
    {
        using var test = new TestDatabase();

        var results = test.Catalog.SearchMedicines("'; DROP TABLE Medicines; --");

        Assert.Empty(results);
        Assert.NotEmpty(test.Catalog.GetMedicines());
    }

    [Fact(DisplayName = "ЕИК с невалидна дължина се отхвърля")]
    public void AddSupplier_ValidatesBulstat()
    {
        using var test = new TestDatabase();

        var supplier = new Supplier
        {
            Code = "9999",
            Name = "Тестов доставчик",
            Bulstat = "12345"
        };

        Assert.Throws<ValidationException>(() => test.Catalog.AddSupplier(test.Manager, supplier));
    }

    [Fact(DisplayName = "Фармацевтът вижда в регистъра само своите продажби")]
    public void SalesJournal_ShowsOnlyOwnDocumentsToPharmacist()
    {
        using var test = new TestDatabase();
        Seed(test);

        var medicine = test.FirstMedicine();
        var today = DateTime.Today;

        // Две продажби от различни служители в един и същи ден.
        test.Sales.RegisterSale(new SaleRequest
        {
            UserId = test.PharmacistId,
            Lines = { new SaleRequestLine(medicine.MedicineId, 2) }
        });

        test.Sales.RegisterSale(new SaleRequest
        {
            UserId = test.ManagerId,
            Lines = { new SaleRequestLine(medicine.MedicineId, 3) }
        });

        var pharmacist = test.Pharmacist;
        var forPharmacist = test.Sales.GetSales(pharmacist, today, today);
        var forManager = test.Sales.GetSales(test.Manager, today, today);

        Assert.Equal(2, forManager.Count);
        Assert.Single(forPharmacist);
        Assert.All(forPharmacist, sale => Assert.Equal(pharmacist.UserId, sale.UserId));
    }

    [Fact(DisplayName = "Фармацевтът не може да отвори чужд документ за продажба")]
    public void GetSale_RejectsForeignDocumentForPharmacist()
    {
        using var test = new TestDatabase();
        Seed(test);

        var medicine = test.FirstMedicine();

        var own = test.Sales.RegisterSale(new SaleRequest
        {
            UserId = test.PharmacistId,
            Lines = { new SaleRequestLine(medicine.MedicineId, 1) }
        });

        var foreign = test.Sales.RegisterSale(new SaleRequest
        {
            UserId = test.ManagerId,
            Lines = { new SaleRequestLine(medicine.MedicineId, 1) }
        });

        var pharmacist = test.Pharmacist;

        Assert.NotNull(test.Sales.GetSale(pharmacist, own.SaleId));
        Assert.Throws<AccessDeniedException>(() => test.Sales.GetSale(pharmacist, foreign.SaleId));

        // Управителят има достъп и до двата документа.
        Assert.NotNull(test.Sales.GetSale(test.Manager, foreign.SaleId));
    }

    [Fact(DisplayName = "Системните настройки се променят само от управител")]
    public void Settings_RequireManagerRole()
    {
        using var test = new TestDatabase();
        var profile = test.SystemSettings.GetProfile();

        Assert.False(string.IsNullOrWhiteSpace(profile.Name));

        // Четенето на данните за обекта е достъпно без ограничение, защото
        // те се показват в заглавната лента и върху документите.
        Assert.Throws<AccessDeniedException>(() =>
            test.SystemSettings.GetAll(test.Pharmacist));

        Assert.Throws<AccessDeniedException>(() =>
            test.SystemSettings.SaveProfile(test.Pharmacist, profile));

        profile.Name = "Аптека „Пример“";
        profile.Address = "гр. Стара Загора, ул. „Първа“ № 1";
        profile.VatRate = 9;
        test.SystemSettings.SaveProfile(test.Manager, profile);

        var saved = test.SystemSettings.GetProfile();
        Assert.Equal("Аптека „Пример“", saved.Name);
        Assert.Equal(9, saved.VatRate);
        Assert.NotEmpty(test.SystemSettings.GetAll(test.Manager));
    }

    [Fact(DisplayName = "Невалидни системни настройки се отхвърлят")]
    public void Settings_RejectInvalidValues()
    {
        using var test = new TestDatabase();
        var manager = test.Manager;

        Assert.Throws<ValidationException>(() => test.SystemSettings.SaveProfile(manager,
            new PharmacyProfile { Name = "   ", VatRate = 20 }));

        Assert.Throws<ValidationException>(() => test.SystemSettings.SaveProfile(manager,
            new PharmacyProfile { Name = "Аптека", VatRate = 120 }));

        // Неуспешният запис не е променил съхранените данни.
        Assert.NotEqual("   ", test.SystemSettings.GetProfile().Name);
    }

    /// <summary>
    /// Заприхождава една партида, за да има наличност, върху която да се
    /// проверява видимостта на стойностните показатели.
    /// </summary>
    private static void Seed(TestDatabase test)
    {
        var medicine = test.FirstMedicine();

        test.Deliveries.RegisterDelivery(new DeliveryRequest
        {
            SupplierId = test.FirstSupplier().SupplierId,
            UserId = test.ManagerId,
            InvoiceNumber = "Ф-1000",
            Lines =
            {
                new DeliveryRequestLine
                {
                    MedicineId = medicine.MedicineId,
                    BatchNumber = "SEED-1",
                    ExpiryDate = DateTime.Today.AddYears(1),
                    Quantity = 40,
                    UnitPrice = 3.10m
                }
            }
        });
    }
}
