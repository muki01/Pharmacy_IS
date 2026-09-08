using System.Data.Common;
using PharmacyIS.Data.Infrastructure;
using PharmacyIS.Domain.Entities;
using PharmacyIS.Domain.Enums;

namespace PharmacyIS.Data.Repositories;

/// <summary>Достъп до данните за потребителите на системата.</summary>
public class UserRepository
{
    private readonly PharmacyDb _db;

    public UserRepository(PharmacyDb db) => _db = db;

    private const string SelectSql = """
        SELECT UserId, Username, PasswordHash, PasswordSalt, FullName, RoleId, IsActive, CreatedAt
        FROM Users
        """;

    /// <summary>Намира потребител по потребителско име.</summary>
    /// <summary>
    /// Търси профил по потребителско име. Сравнението не зависи от регистъра
    /// на буквите, за да не се проваля входът заради включен клавиш Caps Lock.
    /// </summary>
    public User? GetByUsername(string username)
        => _db.QuerySingle(
            $"{SelectSql} WHERE {SqlDialect.Lower(_db.Provider, "Username")} = @username;",
            Map, ("@username", username.Trim().ToLowerInvariant()));

    /// <summary>Намира потребител по първичен ключ.</summary>
    public User? GetById(int userId)
        => _db.QuerySingle($"{SelectSql} WHERE UserId = @id;", Map, ("@id", userId));

    /// <summary>Връща всички потребители, подредени по име.</summary>
    public List<User> GetAll()
        => _db.Query($"{SelectSql} ORDER BY FullName;", Map);

    /// <summary>Проверява дали потребителското име вече е заето.</summary>
    public bool UsernameExists(string username, int excludeUserId = 0)
    {
        var count = Convert.ToInt32(_db.ExecuteScalar(
            $"""
            SELECT COUNT(*) FROM Users
            WHERE {SqlDialect.Lower(_db.Provider, "Username")} = @username AND UserId <> @id;
            """,
            ("@username", username), ("@id", excludeUserId)));

        return count > 0;
    }

    /// <summary>Добавя нов потребител и връща генерирания му идентификатор.</summary>
    public int Insert(User user)
    {
        _db.Execute("""
            INSERT INTO Users (Username, PasswordHash, PasswordSalt, FullName, RoleId, IsActive, CreatedAt)
            VALUES (@username, @hash, @salt, @fullName, @roleId, @isActive, @createdAt);
            """,
            ("@username", user.Username),
            ("@hash", user.PasswordHash),
            ("@salt", user.PasswordSalt),
            ("@fullName", user.FullName),
            ("@roleId", (int)user.Role),
            ("@isActive", user.IsActive),
            ("@createdAt", user.CreatedAt));

        return _db.LastInsertId();
    }

    /// <summary>Обновява данните на потребител без да променя паролата му.</summary>
    public void Update(User user)
    {
        _db.Execute("""
            UPDATE Users
            SET Username = @username, FullName = @fullName, RoleId = @roleId, IsActive = @isActive
            WHERE UserId = @id;
            """,
            ("@username", user.Username),
            ("@fullName", user.FullName),
            ("@roleId", (int)user.Role),
            ("@isActive", user.IsActive),
            ("@id", user.UserId));
    }

    /// <summary>Записва нов отпечатък и нова сол на паролата.</summary>
    public void UpdatePassword(int userId, string hash, string salt)
    {
        _db.Execute(
            "UPDATE Users SET PasswordHash = @hash, PasswordSalt = @salt WHERE UserId = @id;",
            ("@hash", hash), ("@salt", salt), ("@id", userId));
    }

    /// <summary>
    /// Брой активни потребители с роля „Управител“. Използва се, за да не
    /// може системата да остане без нито един управител.
    /// </summary>
    public int CountActiveManagers(int excludeUserId = 0)
    {
        return Convert.ToInt32(_db.ExecuteScalar(
            "SELECT COUNT(*) FROM Users WHERE RoleId = @roleId AND IsActive = 1 AND UserId <> @id;",
            ("@roleId", (int)UserRole.Manager), ("@id", excludeUserId)));
    }

    private static User Map(DbDataReader r) => new()
    {
        UserId = r.GetInt("UserId"),
        Username = r.GetString("Username"),
        PasswordHash = r.GetString("PasswordHash"),
        PasswordSalt = r.GetString("PasswordSalt"),
        FullName = r.GetString("FullName"),
        Role = (UserRole)r.GetInt("RoleId"),
        IsActive = r.GetBool("IsActive"),
        CreatedAt = r.GetDateTime("CreatedAt")
    };
}
