namespace PharmacyIS.Domain;

/// <summary>
/// Паричната единица, в която работи системата.
///
/// Знакът е изнесен на едно място, за да не се повтаря в екраните и
/// документите: смяната на валутата се извършва с промяна само тук.
/// </summary>
public static class Money
{
    /// <summary>Знак на валутата, използван в екраните и документите.</summary>
    public const string Currency = "€";

    /// <summary>Код на валутата по ISO 4217.</summary>
    public const string CurrencyCode = "EUR";

    /// <summary>Наименование на валутата в единствено и множествено число.</summary>
    public const string CurrencyName = "евро";
}
