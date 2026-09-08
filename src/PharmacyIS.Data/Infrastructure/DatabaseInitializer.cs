using System.Reflection;
using System.Text;

namespace PharmacyIS.Data.Infrastructure;

/// <summary>
/// Създава схемата на базата от данни и я зарежда с начални (номенклатурни)
/// данни. Изпълнява се при стартиране на приложението и е идемпотентен –
/// повторното извикване не променя вече съществуваща база.
/// </summary>
public class DatabaseInitializer
{
    private readonly DatabaseSettings _settings;

    public DatabaseInitializer(DatabaseSettings settings)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
    }

    /// <summary>
    /// Осигурява наличието на схемата и на минималния набор от данни,
    /// необходим за работа на системата.
    /// </summary>
    /// <returns><c>true</c>, ако базата е създадена сега (била е празна).</returns>
    public bool EnsureCreated()
    {
        using var db = new PharmacyDb(_settings);

        db.ExecuteScript(ReadSchemaScript());

        var userCount = Convert.ToInt32(db.ExecuteScalar("SELECT COUNT(*) FROM Users;"));
        if (userCount > 0)
            return false;

        db.BeginTransaction();
        try
        {
            SeedRoles(db);
            SeedSettings(db);
            SeedUsers(db);
            SeedDosageForms(db);
            SeedActiveIngredients(db);
            SeedSuppliers(db);
            SeedMedicines(db);
            db.Commit();
        }
        catch
        {
            db.Rollback();
            throw;
        }

        return true;
    }

    /// <summary>Прочита вградения в програмата SQL скрипт със схемата.</summary>
    private string ReadSchemaScript()
    {
        var resourceName = SqlDialect.SchemaResourceName(_settings.Provider);
        var assembly = Assembly.GetExecutingAssembly();

        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException(
                $"Липсва вграден ресурс „{resourceName}“ със схемата на базата от данни.");

        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    // ------------------------------------------------------------------
    // Начални данни
    // ------------------------------------------------------------------

    private static void SeedRoles(PharmacyDb db)
    {
        db.Execute("INSERT INTO Roles (RoleId, Code, Name) VALUES (@id, @code, @name);",
            ("@id", 1), ("@code", "PHARMACIST"), ("@name", "Фармацевт"));

        db.Execute("INSERT INTO Roles (RoleId, Code, Name) VALUES (@id, @code, @name);",
            ("@id", 2), ("@code", "MANAGER"), ("@name", "Управител"));
    }

    private static void SeedSettings(PharmacyDb db)
    {
        var settings = new (string Key, string Value, string Description)[]
        {
            ("ExpiryWarningDays", "60",
                "Брой дни преди изтичане на срока на годност, при които се издава предупреждение."),
            ("PharmacyName", "Аптека „Здраве“",
                "Наименование на аптеката, което се отпечатва върху документите."),
            ("PharmacyAddress", "гр. Стара Загора, ул. „Армейска“ № 11",
                "Адрес на търговския обект."),
            ("VatRate", "20",
                "Ставка на данъка върху добавената стойност в проценти.")
        };

        foreach (var (key, value, description) in settings)
        {
            db.Execute(
                "INSERT INTO Settings (SettingKey, SettingValue, Description) VALUES (@k, @v, @d);",
                ("@k", key), ("@v", value), ("@d", description));
        }
    }

    private static void SeedUsers(PharmacyDb db)
    {
        // Демонстрационни потребители. При въвеждане в реална експлоатация
        // паролите задължително се сменят при първия вход.
        AddUser(db, "manager", "manager123", "Красимира Тодорова Петрова", roleId: 2);
        AddUser(db, "farmacevt", "farmacevt123", "Иван Георгиев Димитров", roleId: 1);
        AddUser(db, "farmacevt2", "farmacevt123", "Мария Стоянова Илиева", roleId: 1);
    }

    private static void AddUser(PharmacyDb db, string username, string password, string fullName, int roleId)
    {
        var salt = PasswordHasher.CreateSalt();
        var hash = PasswordHasher.ComputeHash(password, salt);

        db.Execute(
            """
            INSERT INTO Users (Username, PasswordHash, PasswordSalt, FullName, RoleId, IsActive, CreatedAt)
            VALUES (@username, @hash, @salt, @fullName, @roleId, 1, @createdAt);
            """,
            ("@username", username),
            ("@hash", hash),
            ("@salt", salt),
            ("@fullName", fullName),
            ("@roleId", roleId),
            ("@createdAt", DateTime.Now));
    }

    private static void SeedDosageForms(PharmacyDb db)
    {
        var forms = new (string Code, string Name)[]
        {
            ("01", "Таблетки"),
            ("02", "Капсули"),
            ("03", "Сироп"),
            ("04", "Ампули"),
            ("05", "Крем"),
            ("06", "Мехлем"),
            ("07", "Спрей"),
            ("08", "Капки"),
            ("09", "Супозитории"),
            ("10", "Прах за перорален разтвор"),
            ("11", "Инхалатор"),
            ("12", "Разтвор за външно приложение")
        };

        foreach (var (code, name) in forms)
        {
            db.Execute("INSERT INTO DosageForms (Code, Name) VALUES (@code, @name);",
                ("@code", code), ("@name", name));
        }
    }

    private static void SeedActiveIngredients(PharmacyDb db)
    {
        var ingredients = new (string Code, string Name)[]
        {
            ("001", "Парацетамол"),
            ("002", "Ибупрофен"),
            ("003", "Ацетилсалицилова киселина"),
            ("004", "Амоксицилин"),
            ("005", "Азитромицин"),
            ("006", "Метамизол натрий"),
            ("007", "Цетиризин"),
            ("008", "Лоратадин"),
            ("009", "Омепразол"),
            ("010", "Метформин"),
            ("011", "Еналаприл"),
            ("012", "Амлодипин"),
            ("013", "Аторвастатин"),
            ("014", "Диклофенак"),
            ("015", "Амброксол"),
            ("016", "Аскорбинова киселина"),
            ("017", "Дротаверин"),
            ("018", "Лоперамид"),
            ("019", "Салбутамол"),
            ("020", "Бисопролол")
        };

        foreach (var (code, name) in ingredients)
        {
            db.Execute("INSERT INTO ActiveIngredients (Code, Name) VALUES (@code, @name);",
                ("@code", code), ("@name", name));
        }
    }

    private static void SeedSuppliers(PharmacyDb db)
    {
        var suppliers = new (string Code, string Name, string Bulstat, string Contact, string Phone, string Email, string Address)[]
        {
            ("1001", "Фарма Логистик ЕООД", "201345678", "Николай Иванов",
                "042/620-115", "office@pharmalogistic.bg", "гр. Стара Загора, бул. „Патриарх Евтимий“ № 44"),
            ("1002", "Медика Дистрибюшън АД", "131456789", "Светла Ангелова",
                "02/975-33-40", "sales@medicadist.bg", "гр. София, ж.к. „Дружба“, ул. „Първа“ № 8"),
            ("1003", "Вита Фарм ООД", "123987456", "Петър Костов",
                "032/640-770", "info@vitafarm.bg", "гр. Пловдив, ул. „Брезовска“ № 21"),
            ("2001", "Балкан Фармасютикълс АД", "115098321", "Даниела Маринова",
                "052/300-812", "orders@balkanpharma.bg", "гр. Варна, Западна промишлена зона"),
            ("2002", "Хелт Продукт ЕООД", "205671234", "Георги Атанасов",
                "042/601-448", "office@healthproduct.bg", "гр. Стара Загора, ул. „Индустриална“ № 3")
        };

        foreach (var s in suppliers)
        {
            db.Execute(
                """
                INSERT INTO Suppliers (Code, Name, Bulstat, ContactPerson, Phone, Email, Address, IsActive)
                VALUES (@code, @name, @bulstat, @contact, @phone, @email, @address, 1);
                """,
                ("@code", s.Code), ("@name", s.Name), ("@bulstat", s.Bulstat),
                ("@contact", s.Contact), ("@phone", s.Phone), ("@email", s.Email),
                ("@address", s.Address));
        }
    }

    private static void SeedMedicines(PharmacyDb db)
    {
        // Кодът на продукта е осемразряден: група (2) + активна съставка (3) + продукт (3).
        var medicines = new MedicineSeed[]
        {
            new("01001001", "Парацетамол Актавис 500 mg", "001", "01", "Актавис", "500 mg", 1.64m, false, 40, "3800101010011"),
            new("01001002", "Панадол Екстра", "001", "01", "GSK", "500 mg / 65 mg", 3.53m, false, 25, "3800101010028"),
            new("01001003", "Парацетамол сироп за деца", "001", "03", "Софарма", "120 mg/5 ml", 2.76m, false, 20, "3800101010035"),
            new("01002001", "Ибупрофен 400 mg", "002", "01", "Актавис", "400 mg", 2.45m, false, 35, "3800101020011"),
            new("01002002", "Нурофен за деца", "002", "03", "Reckitt", "100 mg/5 ml", 6.39m, false, 15, "3800101020028"),
            new("01003001", "Аспирин Протект 100 mg", "003", "01", "Bayer", "100 mg", 3.73m, false, 30, "3800101030011"),
            new("01006001", "Аналгин 500 mg", "006", "01", "Софарма", "500 mg", 1.48m, false, 40, "3800101060011"),
            new("01006002", "Аналгин ампули", "006", "04", "Софарма", "500 mg/ml", 2.10m, true, 10, "3800101060028"),
            new("01014001", "Волтарен Емулгел", "014", "05", "Novartis", "10 mg/g", 8.59m, false, 12, "3800101140011"),
            new("01014002", "Диклофенак таблетки 50 mg", "014", "01", "Актавис", "50 mg", 2.86m, true, 20, "3800101140028"),

            new("02004001", "Амоксицилин 500 mg", "004", "02", "Актавис", "500 mg", 4.81m, true, 20, "3800102040011"),
            new("02004002", "Оспамокс 1000 mg", "004", "01", "Sandoz", "1000 mg", 7.26m, true, 15, "3800102040028"),
            new("02005001", "Азитромицин 500 mg", "005", "01", "Teva", "500 mg", 7.00m, true, 15, "3800102050011"),
            new("02005002", "Сумамед прах за суспензия", "005", "10", "Pliva", "200 mg/5 ml", 9.66m, true, 8, "3800102050028"),

            new("03007001", "Цетиризин 10 mg", "007", "01", "Актавис", "10 mg", 3.17m, false, 25, "3800103070011"),
            new("03007002", "Зиртек капки", "007", "08", "UCB", "10 mg/ml", 7.87m, false, 10, "3800103070028"),
            new("03008001", "Лоратадин 10 mg", "008", "01", "Софарма", "10 mg", 2.61m, false, 25, "3800103080011"),

            new("04009001", "Омепразол 20 mg", "009", "02", "Актавис", "20 mg", 4.24m, false, 30, "3800104090011"),
            new("04017001", "Но-Шпа 40 mg", "017", "01", "Sanofi", "40 mg", 4.96m, false, 25, "3800104170011"),
            new("04018001", "Имодиум", "018", "02", "Johnson", "2 mg", 5.78m, false, 15, "3800104180011"),

            new("05010001", "Метформин 850 mg", "010", "01", "Teva", "850 mg", 3.99m, true, 30, "3800105100011"),
            new("05011001", "Еналаприл 10 mg", "011", "01", "Софарма", "10 mg", 2.35m, true, 30, "3800105110011"),
            new("05012001", "Амлодипин 5 mg", "012", "01", "Актавис", "5 mg", 3.02m, true, 30, "3800105120011"),
            new("05013001", "Аторвастатин 20 mg", "013", "01", "Teva", "20 mg", 6.34m, true, 20, "3800105130011"),
            new("05020001", "Бисопролол 5 mg", "020", "01", "Актавис", "5 mg", 3.43m, true, 25, "3800105200011"),

            new("06015001", "Амбробене сироп", "015", "03", "Ratiopharm", "15 mg/5 ml", 5.22m, false, 18, "3800106150011"),
            new("06015002", "Амброксол таблетки 30 mg", "015", "01", "Софарма", "30 mg", 2.51m, false, 20, "3800106150028"),
            new("06019001", "Вентолин инхалатор", "019", "11", "GSK", "100 mcg/доза", 11.04m, true, 8, "3800106190011"),

            new("07016001", "Витамин C 500 mg", "016", "01", "Софарма", "500 mg", 2.20m, false, 40, "3800107160011"),
            new("07016002", "Витамин C ефервесцентни таблетки 1000 mg", "016", "10", "Hermes", "1000 mg", 5.01m, false, 25, "3800107160028")
        };

        foreach (var m in medicines)
        {
            db.Execute(
                """
                INSERT INTO Medicines
                    (Code, Name, IngredientId, FormId, Manufacturer, Strength,
                     Price, RequiresPrescription, MinStock, Barcode, IsActive)
                VALUES
                    (@code, @name,
                     (SELECT IngredientId FROM ActiveIngredients WHERE Code = @ingredientCode),
                     (SELECT FormId FROM DosageForms WHERE Code = @formCode),
                     @manufacturer, @strength, @price, @rx, @minStock, @barcode, 1);
                """,
                ("@code", m.Code),
                ("@name", m.Name),
                ("@ingredientCode", m.IngredientCode),
                ("@formCode", m.FormCode),
                ("@manufacturer", m.Manufacturer),
                ("@strength", m.Strength),
                ("@price", m.Price),
                ("@rx", m.RequiresPrescription),
                ("@minStock", m.MinStock),
                ("@barcode", m.Barcode));
        }
    }

    /// <summary>Помощен запис за началните данни на каталога.</summary>
    private readonly record struct MedicineSeed(
        string Code,
        string Name,
        string IngredientCode,
        string FormCode,
        string Manufacturer,
        string Strength,
        decimal Price,
        bool RequiresPrescription,
        int MinStock,
        string Barcode);
}
