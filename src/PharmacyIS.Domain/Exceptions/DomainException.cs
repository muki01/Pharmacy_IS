namespace PharmacyIS.Domain.Exceptions;

/// <summary>
/// Базово изключение за нарушено бизнес правило. Съобщенията на този тип
/// изключения са предназначени за краен потребител и се извеждат директно
/// в интерфейса, за разлика от техническите грешки.
/// </summary>
public class DomainException : Exception
{
    public DomainException(string message) : base(message) { }
    public DomainException(string message, Exception inner) : base(message, inner) { }
}

/// <summary>Липсва достатъчна наличност за исканата операция.</summary>
public class InsufficientStockException : DomainException
{
    public string MedicineName { get; }
    public int Requested { get; }
    public int Available { get; }

    public InsufficientStockException(string medicineName, int requested, int available)
        : base($"Недостатъчна наличност за „{medicineName}“. Заявени: {requested} бр., налични: {available} бр.")
    {
        MedicineName = medicineName;
        Requested = requested;
        Available = available;
    }
}

/// <summary>Опит за операция с продукт с изтекъл срок на годност.</summary>
public class ExpiredProductException : DomainException
{
    public ExpiredProductException(string medicineName, string batchNumber, DateTime expiry)
        : base($"Партида {batchNumber} на „{medicineName}“ е с изтекъл срок на годност ({expiry:dd.MM.yyyy}) и не може да бъде продадена.")
    { }
}

/// <summary>Нарушено правило за достъп – потребителят няма права за операцията.</summary>
public class AccessDeniedException : DomainException
{
    public AccessDeniedException(string operation)
        : base($"Нямате права за операция „{operation}“. Обърнете се към управителя на аптеката.")
    { }
}

/// <summary>Невалидни входни данни, подадени от потребителя.</summary>
public class ValidationException : DomainException
{
    public ValidationException(string message) : base(message) { }
}
