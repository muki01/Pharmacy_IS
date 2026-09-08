namespace PharmacyIS.Domain.Enums;

/// <summary>Състояние на складов документ (продажба, доставка, протокол за брак).</summary>
public enum DocumentStatus
{
    /// <summary>Приключен и осчетоводен документ.</summary>
    Completed = 1,

    /// <summary>Анулиран документ – количествата са върнати в наличност.</summary>
    Cancelled = 2
}
