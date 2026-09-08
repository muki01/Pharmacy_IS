using PharmacyIS.Domain;
using PharmacyIS.Domain.Reports;
using PharmacyIS.UI.Infrastructure;

namespace PharmacyIS.UI.Forms;

/// <summary>
/// Общ екран за справките на системата. Видът на справката се задава при
/// създаване на екрана и определя колоните, входните параметри и обобщаващия ред.
/// </summary>
public class ReportsForm : BaseChildForm
{
    /// <summary>Видове справки, поддържани от системата.</summary>
    public enum ReportKind
    {
        Stock,
        Expiry,
        DailyTurnover,
        TopProducts,
        SalesByUser,
        DeliveriesBySupplier,
        WriteOffs
    }

    /// <summary>Видът на показваната справка.</summary>
    public ReportKind Kind { get; }

    private readonly DateTimePicker _from = new();
    private readonly DateTimePicker _to = new();
    private readonly NumericUpDown _days = new();
    private readonly CheckBox _onlyBelowMinimum = new();
    private readonly DataGridView _grid = new();
    private readonly Label _summary = UiHelpers.SummaryLabel(emphasised: true);
    private readonly FlowLayoutPanel _parameters = UiHelpers.CreateFilterBar();

    /// <summary>Текущата роля има ли достъп до стойностните показатели.</summary>
    private readonly bool _showFinancials = AppSession.IsManager;

    public ReportsForm(ReportKind kind)
        : base(GetTitle(kind), GetSubtitle(kind))
    {
        Kind = kind;

        AddToolbarButton("Изнеси в CSV", () => UiHelpers.ExportGridToCsv(this, _grid, GetTitle(kind)), 150);
        AddToolbarButton("Обнови", LoadData, 110);

        BuildParameters();
        BuildGrid();

        Load += (_, _) => Run(LoadData);
    }

    public override void OnPageActivated() => Run(LoadData);

    private static string GetTitle(ReportKind kind) => kind switch
    {
        ReportKind.Stock => "Складова наличност",
        ReportKind.Expiry => "Изтичащи срокове на годност",
        ReportKind.DailyTurnover => "Дневен оборот",
        ReportKind.TopProducts => "Най-продавани продукти",
        ReportKind.SalesByUser => "Продажби по служители",
        ReportKind.DeliveriesBySupplier => "Доставки по доставчици",
        ReportKind.WriteOffs => "Бракувани количества",
        _ => "Справка"
    };

    private static string GetSubtitle(ReportKind kind) => kind switch
    {
        ReportKind.Stock => AppSession.IsManager
            ? "Налични количества, минимални запаси и отчетна стойност"
            : "Налични количества, минимални запаси и най-близки срокове",
        ReportKind.Expiry => "Партиди, чийто срок изтича в зададения период",
        ReportKind.DailyTurnover => "Обобщение на продажбите по календарни дни",
        ReportKind.TopProducts => "Класация по продадено количество и оборот",
        ReportKind.SalesByUser => "Оборот и среден размер на покупката по служители",
        ReportKind.DeliveriesBySupplier => "Доставени количества и стойност по контрагенти",
        ReportKind.WriteOffs => "Бракувани количества и основания за брак",
        _ => string.Empty
    };

    // ------------------------------------------------------------------
    //  Параметри на справката
    // ------------------------------------------------------------------

    private void BuildParameters()
    {
        var needsPeriod = Kind is ReportKind.DailyTurnover or ReportKind.TopProducts
            or ReportKind.SalesByUser or ReportKind.DeliveriesBySupplier or ReportKind.WriteOffs;

        if (needsPeriod)
        {
            UiHelpers.SetupPeriod(_from, _to, daysBack: 30, () => Run(LoadData));

            _parameters.Controls.Add(UiHelpers.LabeledField("От дата", _from, 140));
            _parameters.Controls.Add(UiHelpers.LabeledField("До дата", _to, 140));
        }

        if (Kind == ReportKind.Expiry)
        {
            _days.Minimum = 0;
            _days.Maximum = 3650;
            _days.Value = 90;
            _days.ValueChanged += (_, _) => Run(LoadData);
            _parameters.Controls.Add(UiHelpers.LabeledField("Дни напред", _days, 110));
        }

        if (Kind == ReportKind.Stock)
        {
            _onlyBelowMinimum.AutoSize = true;
            _onlyBelowMinimum.Text = "Само продукти под минималния запас";
            _onlyBelowMinimum.CheckedChanged += (_, _) => Run(LoadData);
            _parameters.Controls.Add(UiHelpers.BareField(_onlyBelowMinimum));
        }
    }

    // ------------------------------------------------------------------
    //  Колони
    // ------------------------------------------------------------------

    private void BuildGrid()
    {
        UiHelpers.StyleGrid(_grid);
        _grid.Dock = DockStyle.Fill;

        switch (Kind)
        {
            case ReportKind.Stock:
                _grid.Columns.AddRange(
                    UiHelpers.TextColumn(nameof(StockRow.MedicineCode), "Код", 60),
                    UiHelpers.TextColumn(nameof(StockRow.MedicineName), "Наименование", 220),
                    UiHelpers.TextColumn(nameof(StockRow.FormName), "Форма", 90),
                    UiHelpers.TextColumn(nameof(StockRow.Quantity), "Налично", 65, rightAligned: true),
                    UiHelpers.TextColumn(nameof(StockRow.MinStock), "Минимум", 65, rightAligned: true),
                    UiHelpers.TextColumn(nameof(StockRow.RetailPrice), "Продажна цена", 85, "N2", rightAligned: true),
                    UiHelpers.TextColumn(nameof(StockRow.NearestExpiry), "Най-близък срок", 95, "dd.MM.yyyy"));

                // Отчетната стойност се показва само на роли с достъп до
                // финансовите показатели на аптеката.
                if (_showFinancials)
                {
                    _grid.Columns.Insert(6, UiHelpers.TextColumn(
                        nameof(StockRow.StockValue), "Отчетна стойност", 100, "N2", rightAligned: true));
                }
                break;

            case ReportKind.Expiry:
                _grid.Columns.AddRange(
                    UiHelpers.TextColumn(nameof(ExpiryRow.MedicineCode), "Код", 60),
                    UiHelpers.TextColumn(nameof(ExpiryRow.MedicineName), "Наименование", 220),
                    UiHelpers.TextColumn(nameof(ExpiryRow.BatchNumber), "Партида", 90),
                    UiHelpers.TextColumn(nameof(ExpiryRow.ExpiryDate), "Срок на годност", 95, "dd.MM.yyyy"),
                    UiHelpers.TextColumn(nameof(ExpiryRow.DaysLeft), "Оставащи дни", 85, rightAligned: true),
                    UiHelpers.TextColumn(nameof(ExpiryRow.Quantity), "Количество", 75, rightAligned: true));

                if (_showFinancials)
                {
                    _grid.Columns.Add(UiHelpers.TextColumn(
                        nameof(ExpiryRow.Value), "Стойност", 85, "N2", rightAligned: true));
                }

                _grid.CellFormatting += (_, e) =>
                {
                    if (_grid.Rows[e.RowIndex].DataBoundItem is not ExpiryRow row) return;

                    _grid.Rows[e.RowIndex].DefaultCellStyle.BackColor =
                        row.DaysLeft < 0 ? UiHelpers.Danger : UiHelpers.Warning;
                };
                break;

            case ReportKind.DailyTurnover:
                _grid.Columns.AddRange(
                    UiHelpers.TextColumn(nameof(DailyTurnoverRow.Date), "Дата", 100, "dd.MM.yyyy"),
                    UiHelpers.TextColumn(nameof(DailyTurnoverRow.SalesCount), "Продажби", 80, rightAligned: true),
                    UiHelpers.TextColumn(nameof(DailyTurnoverRow.ItemsSold), "Продадени бр.", 90, rightAligned: true),
                    UiHelpers.TextColumn(nameof(DailyTurnoverRow.Turnover), $"Оборот, {Money.Currency}", 100, "N2", rightAligned: true),
                    UiHelpers.TextColumn(nameof(DailyTurnoverRow.Cost), $"Себестойност, {Money.Currency}", 110, "N2", rightAligned: true),
                    UiHelpers.TextColumn(nameof(DailyTurnoverRow.Profit), $"Печалба, {Money.Currency}", 100, "N2", rightAligned: true));
                break;

            case ReportKind.TopProducts:
                _grid.Columns.AddRange(
                    UiHelpers.TextColumn(nameof(TopProductRow.MedicineCode), "Код", 60),
                    UiHelpers.TextColumn(nameof(TopProductRow.MedicineName), "Наименование", 220),
                    UiHelpers.TextColumn(nameof(TopProductRow.IngredientName), "Активна съставка", 150),
                    UiHelpers.TextColumn(nameof(TopProductRow.QuantitySold), "Продадени бр.", 90, rightAligned: true),
                    UiHelpers.TextColumn(nameof(TopProductRow.Turnover), $"Оборот, {Money.Currency}", 95, "N2", rightAligned: true),
                    UiHelpers.TextColumn(nameof(TopProductRow.Profit), $"Печалба, {Money.Currency}", 95, "N2", rightAligned: true));
                break;

            case ReportKind.SalesByUser:
                _grid.Columns.AddRange(
                    UiHelpers.TextColumn(nameof(SalesByUserRow.UserName), "Служител", 220),
                    UiHelpers.TextColumn(nameof(SalesByUserRow.RoleName), "Роля", 110),
                    UiHelpers.TextColumn(nameof(SalesByUserRow.SalesCount), "Продажби", 90, rightAligned: true),
                    UiHelpers.TextColumn(nameof(SalesByUserRow.Turnover), $"Оборот, {Money.Currency}", 110, "N2", rightAligned: true),
                    UiHelpers.TextColumn(nameof(SalesByUserRow.AverageReceipt), $"Средна покупка, {Money.Currency}", 120, "N2", rightAligned: true));
                break;

            case ReportKind.DeliveriesBySupplier:
                _grid.Columns.AddRange(
                    UiHelpers.TextColumn(nameof(DeliveriesBySupplierRow.SupplierCode), "Код", 55),
                    UiHelpers.TextColumn(nameof(DeliveriesBySupplierRow.SupplierName), "Доставчик", 200),
                    UiHelpers.TextColumn(nameof(DeliveriesBySupplierRow.MedicineCode), "Код прод.", 70),
                    UiHelpers.TextColumn(nameof(DeliveriesBySupplierRow.MedicineName), "Продукт", 220),
                    UiHelpers.TextColumn(nameof(DeliveriesBySupplierRow.Quantity), "Количество", 80, rightAligned: true),
                    UiHelpers.TextColumn(nameof(DeliveriesBySupplierRow.Amount), $"Стойност, {Money.Currency}", 100, "N2", rightAligned: true));
                break;

            case ReportKind.WriteOffs:
                _grid.Columns.AddRange(
                    UiHelpers.TextColumn(nameof(WriteOffRow.Date), "Дата", 95, "dd.MM.yyyy"),
                    UiHelpers.TextColumn(nameof(WriteOffRow.DocNumber), "Протокол №", 120),
                    UiHelpers.TextColumn(nameof(WriteOffRow.MedicineName), "Продукт", 200),
                    UiHelpers.TextColumn(nameof(WriteOffRow.BatchNumber), "Партида", 85),
                    UiHelpers.TextColumn(nameof(WriteOffRow.Quantity), "Количество", 75, rightAligned: true),
                    UiHelpers.TextColumn(nameof(WriteOffRow.Value), $"Стойност, {Money.Currency}", 90, "N2", rightAligned: true),
                    UiHelpers.TextColumn(nameof(WriteOffRow.Reason), "Основание", 180));
                break;
        }

        // Редът на добавяне определя реда на подреждане: контролата с
        // запълване се добавя първа, а прикрепените към ръбовете – след нея.
        Content.Controls.Add(_grid);
        Content.Controls.Add(_summary);
        Content.Controls.Add(_parameters);
    }

    // ------------------------------------------------------------------
    //  Извличане на данните
    // ------------------------------------------------------------------

    private void LoadData()
    {
        var user = AppSession.CurrentUser!;
        var from = _from.Value;
        var to = _to.Value;

        switch (Kind)
        {
            case ReportKind.Stock:
            {
                var rows = AppSession.Reports.GetStockReport(user, _onlyBelowMinimum.Checked);
                UiHelpers.Bind(_grid, rows);
                _summary.Text = $"Продукти: {rows.Count} · " +
                                $"Общо количество: {rows.Sum(r => r.Quantity)} бр." +
                                (_showFinancials
                                    ? $" · Отчетна стойност: {rows.Sum(r => r.StockValue):N2} {Money.Currency}"
                                    : string.Empty);
                break;
            }

            case ReportKind.Expiry:
            {
                var rows = AppSession.Reports.GetExpiryReport(user, (int)_days.Value);
                UiHelpers.Bind(_grid, rows);
                _summary.Text = $"Партиди: {rows.Count} · " +
                                $"Количество: {rows.Sum(r => r.Quantity)} бр." +
                                (_showFinancials
                                    ? $" · Стойност под риск: {rows.Sum(r => r.Value):N2} {Money.Currency}"
                                    : string.Empty);
                break;
            }

            case ReportKind.DailyTurnover:
            {
                var rows = AppSession.Reports.GetDailyTurnover(user, from, to);
                UiHelpers.Bind(_grid, rows);
                _summary.Text = $"Дни с продажби: {rows.Count} · " +
                                $"Общ оборот: {rows.Sum(r => r.Turnover):N2} {Money.Currency} · " +
                                $"Обща печалба: {rows.Sum(r => r.Profit):N2} {Money.Currency}";
                break;
            }

            case ReportKind.TopProducts:
            {
                var rows = AppSession.Reports.GetTopProducts(user, from, to, 30);
                UiHelpers.Bind(_grid, rows);
                _summary.Text = $"Позиции: {rows.Count} · " +
                                $"Продадени: {rows.Sum(r => r.QuantitySold)} бр. · " +
                                $"Оборот: {rows.Sum(r => r.Turnover):N2} {Money.Currency}";
                break;
            }

            case ReportKind.SalesByUser:
            {
                var rows = AppSession.Reports.GetSalesByUser(user, from, to);
                UiHelpers.Bind(_grid, rows);
                _summary.Text = $"Служители: {rows.Count} · " +
                                $"Продажби: {rows.Sum(r => r.SalesCount)} · " +
                                $"Оборот: {rows.Sum(r => r.Turnover):N2} {Money.Currency}";
                break;
            }

            case ReportKind.DeliveriesBySupplier:
            {
                var rows = AppSession.Reports.GetDeliveriesBySupplier(user, from, to);
                UiHelpers.Bind(_grid, rows);
                _summary.Text = $"Позиции: {rows.Count} · " +
                                $"Доставени: {rows.Sum(r => r.Quantity)} бр. · " +
                                $"Стойност: {rows.Sum(r => r.Amount):N2} {Money.Currency}";
                break;
            }

            case ReportKind.WriteOffs:
            {
                var rows = AppSession.Reports.GetWriteOffReport(user, from, to);
                UiHelpers.Bind(_grid, rows);
                _summary.Text = $"Записи: {rows.Count} · " +
                                $"Бракувани: {rows.Sum(r => r.Quantity)} бр. · " +
                                $"Загуба: {rows.Sum(r => r.Value):N2} {Money.Currency}";
                break;
            }
        }
    }
}
