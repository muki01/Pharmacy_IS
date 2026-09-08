using PharmacyIS.Data.Infrastructure;
using PharmacyIS.Data.Repositories;
using PharmacyIS.Domain.Entities;
using PharmacyIS.Domain.Exceptions;
using PharmacyIS.Domain.Reports;

namespace PharmacyIS.Services;

/// <summary>
/// Извеждане на справките на системата.
///
/// Справките, които съдържат финансови резултати (оборот, себестойност,
/// печалба), са достъпни само за потребители с роля „Управител“.
/// Оперативните справки за наличности и срокове са достъпни за всички.
/// </summary>
public class ReportService
{
    private readonly DbSessionFactory _sessions;

    public ReportService(DbSessionFactory sessions) => _sessions = sessions;

    /// <summary>Справка „Дневен оборот“ за период. Само за управител.</summary>
    public List<DailyTurnoverRow> GetDailyTurnover(User currentUser, DateTime from, DateTime to)
    {
        AuthService.RequireManager(currentUser, "Справка „Дневен оборот“");
        ValidatePeriod(from, to);

        using var db = _sessions.Create();
        return new ReportRepository(db).GetDailyTurnover(from, to);
    }

    /// <summary>Справка „Най-продавани продукти“. Само за управител.</summary>
    public List<TopProductRow> GetTopProducts(User currentUser, DateTime from, DateTime to, int limit = 20)
    {
        AuthService.RequireManager(currentUser, "Справка „Най-продавани продукти“");
        ValidatePeriod(from, to);

        if (limit is < 1 or > 500)
            throw new ValidationException("Броят на редовете трябва да е между 1 и 500.");

        using var db = _sessions.Create();
        return new ReportRepository(db).GetTopProducts(from, to, limit);
    }

    /// <summary>Справка „Продажби по служители“. Само за управител.</summary>
    public List<SalesByUserRow> GetSalesByUser(User currentUser, DateTime from, DateTime to)
    {
        AuthService.RequireManager(currentUser, "Справка „Продажби по служители“");
        ValidatePeriod(from, to);

        using var db = _sessions.Create();
        return new ReportRepository(db).GetSalesByUser(from, to);
    }

    /// <summary>Справка „Доставени количества по доставчици“. Само за управител.</summary>
    public List<DeliveriesBySupplierRow> GetDeliveriesBySupplier(User currentUser,
        DateTime from, DateTime to, int? supplierId = null)
    {
        AuthService.RequireManager(currentUser, "Справка „Доставки по доставчици“");
        ValidatePeriod(from, to);

        using var db = _sessions.Create();
        return new ReportRepository(db).GetDeliveriesBySupplier(from, to, supplierId);
    }

    /// <summary>
    /// Справка „Складова наличност“ – оперативна справка, достъпна за всички
    /// служители. Отчетната стойност на наличността се попълва само за роли с
    /// достъп до финансова информация.
    /// </summary>
    public List<StockRow> GetStockReport(User currentUser, bool onlyBelowMinimum = false)
    {
        using var db = _sessions.Create();
        var rows = new ReportRepository(db).GetStockReport(onlyBelowMinimum);

        if (!AuthService.CanSeeFinancialData(currentUser))
            foreach (var row in rows) row.StockValue = 0m;

        return rows;
    }

    /// <summary>
    /// Справка „Изтичащи срокове на годност“ – оперативна справка, достъпна за
    /// всички служители. Стойността на застрашените количества се попълва само
    /// за роли с достъп до финансова информация.
    /// </summary>
    public List<ExpiryRow> GetExpiryReport(User currentUser, int daysAhead, DateTime? asOf = null)
    {
        if (daysAhead is < 0 or > 3650)
            throw new ValidationException("Периодът трябва да е между 0 и 3650 дни.");

        using var db = _sessions.Create();
        var rows = new ReportRepository(db).GetExpiryReport(asOf ?? DateTime.Now, daysAhead);

        if (!AuthService.CanSeeFinancialData(currentUser))
            foreach (var row in rows) row.Value = 0m;

        return rows;
    }

    /// <summary>Справка „Бракувани количества“. Само за управител.</summary>
    public List<WriteOffRow> GetWriteOffReport(User currentUser, DateTime from, DateTime to)
    {
        AuthService.RequireManager(currentUser, "Справка „Бракувани количества“");
        ValidatePeriod(from, to);

        using var db = _sessions.Create();
        return new ReportRepository(db).GetWriteOffReport(from, to);
    }

    /// <summary>Проверява коректността на зададения период.</summary>
    private static void ValidatePeriod(DateTime from, DateTime to)
    {
        if (from.Date > to.Date)
            throw new ValidationException("Началната дата на периода е след крайната.");

        if ((to.Date - from.Date).TotalDays > 3650)
            throw new ValidationException("Максималната продължителност на периода е 10 години.");
    }
}
