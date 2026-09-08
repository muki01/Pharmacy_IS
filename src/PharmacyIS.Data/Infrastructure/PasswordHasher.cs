using System.Security.Cryptography;

namespace PharmacyIS.Data.Infrastructure;

/// <summary>
/// Еднопосочно преобразуване на пароли по алгоритъм PBKDF2 (RFC 2898)
/// с хеш-функция SHA-256.
///
/// В базата от данни не се съхранява паролата, а само нейният отпечатък
/// и индивидуална случайна сол. Дори при неоторизиран достъп до базата
/// оригиналните пароли не могат да бъдат възстановени, а еднаквите пароли
/// на различни потребители дават различни отпечатъци.
/// </summary>
public static class PasswordHasher
{
    /// <summary>Дължина на солта в байтове.</summary>
    private const int SaltSize = 16;

    /// <summary>Дължина на генерирания отпечатък в байтове.</summary>
    private const int HashSize = 32;

    /// <summary>
    /// Брой итерации на алгоритъма. Голямата стойност умишлено забавя
    /// изчислението и оскъпява атаките с изчерпване на всички възможности.
    /// </summary>
    private const int Iterations = 100_000;

    /// <summary>Създава нова случайна сол.</summary>
    public static string CreateSalt()
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        return Convert.ToBase64String(salt);
    }

    /// <summary>Изчислява отпечатъка на паролата за подадената сол.</summary>
    public static string ComputeHash(string password, string saltBase64)
    {
        ArgumentException.ThrowIfNullOrEmpty(password);
        ArgumentException.ThrowIfNullOrEmpty(saltBase64);

        var salt = Convert.FromBase64String(saltBase64);
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            password: password,
            salt: salt,
            iterations: Iterations,
            hashAlgorithm: HashAlgorithmName.SHA256,
            outputLength: HashSize);

        return Convert.ToBase64String(hash);
    }

    /// <summary>
    /// Проверява дали подадената парола отговаря на съхранения отпечатък.
    /// Сравнението е с постоянно време, за да не позволява атака по
    /// измерване на времето за отговор.
    /// </summary>
    public static bool Verify(string password, string saltBase64, string expectedHashBase64)
    {
        if (string.IsNullOrEmpty(password) ||
            string.IsNullOrEmpty(saltBase64) ||
            string.IsNullOrEmpty(expectedHashBase64))
        {
            return false;
        }

        try
        {
            var actual = Convert.FromBase64String(ComputeHash(password, saltBase64));
            var expected = Convert.FromBase64String(expectedHashBase64);
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        catch (FormatException)
        {
            // Повреден запис в базата – третира се като неуспешна проверка.
            return false;
        }
    }
}
