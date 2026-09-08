using PharmacyIS.Data.Infrastructure;
using PharmacyIS.Data.Repositories;
using PharmacyIS.Domain.Entities;
using PharmacyIS.Domain.Enums;
using PharmacyIS.Domain.Exceptions;

namespace PharmacyIS.Services;

/// <summary>
/// Удостоверяване на потребителите и администриране на потребителските профили.
/// </summary>
public class AuthService
{
    private readonly DbSessionFactory _sessions;

    /// <summary>Допустим брой неуспешни опита за вход преди временно блокиране.</summary>
    public const int MaxFailedAttempts = 5;

    /// <summary>Продължителност на прозореца за отчитане на неуспешните опити, в минути.</summary>
    public const int LockoutWindowMinutes = 10;

    /// <summary>Минимална дължина на паролата.</summary>
    public const int MinPasswordLength = 8;

    public AuthService(DbSessionFactory sessions) => _sessions = sessions;

    /// <summary>
    /// Проверява подадените данни за вход и връща потребителя при успех.
    /// Всеки опит – успешен или не – се записва в одитния дневник.
    /// </summary>
    /// <exception cref="DomainException">При невалидни данни или блокиран профил.</exception>
    public User Login(string username, string password)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrEmpty(password))
            throw new ValidationException("Въведете потребителско име и парола.");

        // Потребителското име се привежда към малки букви преди всяка
        // проверка. Без това броенето на неуспешните опити щеше да се води
        // поотделно за „ivan“, „Ivan“ и „IVAN“ и временното блокиране би
        // могло да бъде заобиколено чрез редуване на регистъра.
        username = username.Trim().ToLowerInvariant();

        using var db = _sessions.Create();
        var audit = new AuditRepository(db);
        var users = new UserRepository(db);

        // Защита срещу автоматизиран подбор на пароли.
        var failed = audit.CountRecentFailedLogins(username, LockoutWindowMinutes);
        if (failed >= MaxFailedAttempts)
        {
            audit.Log(username, "LOGIN_BLOCKED", false,
                $"Превишен брой неуспешни опити: {failed}");

            throw new DomainException(
                $"Профилът е временно блокиран поради {failed} неуспешни опита за вход. " +
                $"Опитайте отново след {LockoutWindowMinutes} минути.");
        }

        var user = users.GetByUsername(username);

        // Проверката на паролата се извършва и при несъществуващ потребител,
        // за да не се различава времето за отговор в двата случая.
        var dummySalt = PasswordHasher.CreateSalt();
        var isValid = user is not null
            ? PasswordHasher.Verify(password, user.PasswordSalt, user.PasswordHash)
            : PasswordHasher.Verify(password, dummySalt, PasswordHasher.ComputeHash("-", dummySalt));

        if (user is null || !isValid)
        {
            audit.Log(username, "LOGIN_FAILED", false, "Грешно потребителско име или парола.");
            throw new DomainException("Грешно потребителско име или парола.");
        }

        if (!user.IsActive)
        {
            audit.Log(username, "LOGIN_INACTIVE", false, "Опит за вход с деактивиран профил.");
            throw new DomainException("Профилът е деактивиран. Обърнете се към управителя на аптеката.");
        }

        audit.Log(username, "LOGIN_SUCCESS", true, $"Роля: {user.RoleName}");
        return user;
    }

    /// <summary>Смяна на собствената парола на потребителя.</summary>
    public void ChangePassword(int userId, string currentPassword, string newPassword)
    {
        ValidatePasswordStrength(newPassword);

        using var db = _sessions.Create();
        var users = new UserRepository(db);
        var audit = new AuditRepository(db);

        var user = users.GetById(userId)
            ?? throw new DomainException("Потребителят не е намерен.");

        if (!PasswordHasher.Verify(currentPassword, user.PasswordSalt, user.PasswordHash))
        {
            audit.Log(user.Username, "PASSWORD_CHANGE_FAILED", false, "Грешна текуща парола.");
            throw new ValidationException("Текущата парола е грешна.");
        }

        var salt = PasswordHasher.CreateSalt();
        users.UpdatePassword(userId, PasswordHasher.ComputeHash(newPassword, salt), salt);
        audit.Log(user.Username, "PASSWORD_CHANGED", true);
    }

    /// <summary>Връща всички потребители. Достъпно само за управител.</summary>
    public List<User> GetAllUsers(User currentUser)
    {
        RequireManager(currentUser, "Преглед на потребителите");

        using var db = _sessions.Create();
        return new UserRepository(db).GetAll();
    }

    /// <summary>Създава нов потребителски профил. Достъпно само за управител.</summary>
    public int CreateUser(User currentUser, User newUser, string password)
    {
        RequireManager(currentUser, "Създаване на потребител");
        ValidateUser(newUser);
        ValidatePasswordStrength(password);

        using var db = _sessions.Create();
        var users = new UserRepository(db);

        if (users.UsernameExists(newUser.Username))
            throw new ValidationException($"Потребителското име „{newUser.Username}“ вече се използва.");

        newUser.PasswordSalt = PasswordHasher.CreateSalt();
        newUser.PasswordHash = PasswordHasher.ComputeHash(password, newUser.PasswordSalt);
        newUser.CreatedAt = DateTime.Now;

        var id = users.Insert(newUser);
        new AuditRepository(db).Log(currentUser.Username, "USER_CREATED", true,
            $"Създаден профил „{newUser.Username}“ с роля {newUser.RoleName}.");

        return id;
    }

    /// <summary>Обновява данните на потребител. Достъпно само за управител.</summary>
    public void UpdateUser(User currentUser, User user)
    {
        RequireManager(currentUser, "Редактиране на потребител");
        ValidateUser(user);

        using var db = _sessions.Create();
        var users = new UserRepository(db);

        if (users.UsernameExists(user.Username, user.UserId))
            throw new ValidationException($"Потребителското име „{user.Username}“ вече се използва.");

        // Системата не трябва да остава без нито един активен управител.
        var willLoseManager = (!user.IsActive || user.Role != UserRole.Manager)
                              && users.CountActiveManagers(user.UserId) == 0;

        if (willLoseManager)
        {
            throw new ValidationException(
                "Операцията не е позволена – в системата трябва да остане поне един активен управител.");
        }

        users.Update(user);
        new AuditRepository(db).Log(currentUser.Username, "USER_UPDATED", true,
            $"Променен профил „{user.Username}“.");
    }

    /// <summary>Задава нова парола на друг потребител. Достъпно само за управител.</summary>
    public void ResetPassword(User currentUser, int userId, string newPassword)
    {
        RequireManager(currentUser, "Смяна на чужда парола");
        ValidatePasswordStrength(newPassword);

        using var db = _sessions.Create();
        var users = new UserRepository(db);

        var user = users.GetById(userId)
            ?? throw new DomainException("Потребителят не е намерен.");

        var salt = PasswordHasher.CreateSalt();
        users.UpdatePassword(userId, PasswordHasher.ComputeHash(newPassword, salt), salt);

        new AuditRepository(db).Log(currentUser.Username, "PASSWORD_RESET", true,
            $"Зададена нова парола на „{user.Username}“.");
    }

    /// <summary>Последните записи от одитния дневник. Достъпно само за управител.</summary>
    public List<AuditLogEntry> GetAuditLog(User currentUser, int count = 200)
    {
        RequireManager(currentUser, "Преглед на одитния дневник");

        using var db = _sessions.Create();
        return new AuditRepository(db).GetRecent(count);
    }

    // ------------------------------------------------------------------
    // Проверки
    // ------------------------------------------------------------------

    /// <summary>Изисква ролята „Управител“ за извършване на операцията.</summary>
    public static void RequireManager(User user, string operation)
    {
        if (user is null || user.Role != UserRole.Manager)
            throw new AccessDeniedException(operation);
    }

    /// <summary>
    /// Показва дали ролята има право да вижда стойностни (финансови) данни –
    /// доставни цени, себестойност, отчетна стойност на склада, оборот и печалба.
    ///
    /// Фармацевтът работи с количества и с продажни цени, които така или иначе
    /// са обявени пред клиента. Обобщената финансова картина на аптеката е
    /// търговска информация и остава достъпна само за управителя.
    /// </summary>
    public static bool CanSeeFinancialData(User? user) => user is { Role: UserRole.Manager };

    /// <summary>Проверява минималните изисквания към сложността на паролата.</summary>
    public static void ValidatePasswordStrength(string password)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < MinPasswordLength)
            throw new ValidationException($"Паролата трябва да е дълга поне {MinPasswordLength} знака.");

        if (!password.Any(char.IsLetter) || !password.Any(char.IsDigit))
            throw new ValidationException("Паролата трябва да съдържа поне една буква и поне една цифра.");
    }

    private static void ValidateUser(User user)
    {
        if (string.IsNullOrWhiteSpace(user.Username) || user.Username.Trim().Length < 3)
            throw new ValidationException("Потребителското име трябва да е дълго поне 3 знака.");

        if (user.Username.Trim().Any(char.IsWhiteSpace))
            throw new ValidationException("Потребителското име не може да съдържа интервали.");

        if (string.IsNullOrWhiteSpace(user.FullName))
            throw new ValidationException("Въведете име и фамилия на служителя.");

        // Имената се съхраняват с малки букви, за да съвпадат с начина, по
        // който се сравняват при вход и в одитния дневник.
        user.Username = user.Username.Trim().ToLowerInvariant();
        user.FullName = user.FullName.Trim();
    }
}
