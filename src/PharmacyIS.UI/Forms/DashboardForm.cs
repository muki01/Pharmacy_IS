using PharmacyIS.Domain;
using PharmacyIS.Domain.Entities;
using PharmacyIS.Domain.Enums;
using PharmacyIS.Domain.Reports;
using PharmacyIS.UI.Infrastructure;

namespace PharmacyIS.UI.Forms;

/// <summary>
/// Начална страница. Извежда обобщени показатели за деня и предупрежденията
/// за ниска наличност и изтичащи срокове на годност – съгласно изискването
/// предупрежденията да се показват веднага след стартиране.
/// </summary>
public class DashboardForm : BaseChildForm
{
    private readonly Action _onAlertsChanged;
    private readonly Action<string> _navigate;

    private readonly FlowLayoutPanel _tiles = new()
    {
        Dock = DockStyle.Top,
        AutoSize = true,
        AutoSizeMode = AutoSizeMode.GrowAndShrink,
        WrapContents = true,
        Padding = new Padding(0, 0, 0, 14),
        Margin = new Padding(0)
    };

    private readonly DataGridView _lowStockGrid = new();
    private readonly DataGridView _expiryGrid = new();
    private readonly Label _lowStockTitle = UiHelpers.SectionTitle("Продукти под минималния запас");
    private readonly Label _expiryTitle = UiHelpers.SectionTitle("Партиди с изтичащ срок на годност");

    public DashboardForm(Action onAlertsChanged, Action<string> navigate)
        : base("Начало", "Обобщени показатели за деня и активни предупреждения")
    {
        _onAlertsChanged = onAlertsChanged;
        _navigate = navigate;

        AddToolbarButton("Нова продажба", () => _navigate("sale"), 150, primary: true);
        AddToolbarButton("Нова доставка", () => _navigate("delivery"), 150);
        AddToolbarButton("Складови наличности", () => _navigate("stock"), 180);
        AddToolbarSeparator();
        AddToolbarButton("Обнови", LoadData, 110);

        // Бракуването на изтекли количества е складова операция и се
        // извършва от служителя, който установява негодната стока.
        AddToolbarButton("Бракувай изтеклите", WriteOffExpired, 180);

        BuildLayout();
        Load += (_, _) => Run(LoadData);
    }

    public override void OnPageActivated() => Run(LoadData);

    private void BuildLayout()
    {
        var split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            SplitterWidth = 16,
            BackColor = Color.White
        };

        ConfigureLowStockGrid();
        ConfigureExpiryGrid();

        split.Panel1.Controls.Add(_lowStockGrid);
        split.Panel1.Controls.Add(_lowStockTitle);
        split.Panel2.Controls.Add(_expiryGrid);
        split.Panel2.Controls.Add(_expiryTitle);
        UiHelpers.KeepSplitRatio(split, 0.5);

        Content.Controls.Add(split);
        Content.Controls.Add(_tiles);
    }

    private void ConfigureLowStockGrid()
    {
        UiHelpers.StyleGrid(_lowStockGrid);
        _lowStockGrid.Dock = DockStyle.Fill;
        _lowStockGrid.Columns.AddRange(
            UiHelpers.TextColumn(nameof(StockRow.MedicineCode), "Код", 60),
            UiHelpers.TextColumn(nameof(StockRow.MedicineName), "Наименование", 200),
            UiHelpers.TextColumn(nameof(StockRow.Quantity), "Налично", 75, rightAligned: true),
            UiHelpers.TextColumn(nameof(StockRow.MinStock), "Минимум", 78, rightAligned: true));

        _lowStockGrid.CellFormatting += (_, e) =>
        {
            if (_lowStockGrid.Rows[e.RowIndex].DataBoundItem is not StockRow row) return;

            _lowStockGrid.Rows[e.RowIndex].DefaultCellStyle.BackColor =
                row.Quantity == 0 ? UiHelpers.Danger : UiHelpers.Warning;
        };
    }

    private void ConfigureExpiryGrid()
    {
        UiHelpers.StyleGrid(_expiryGrid);
        _expiryGrid.Dock = DockStyle.Fill;
        _expiryGrid.Columns.AddRange(
            UiHelpers.TextColumn(nameof(Batch.MedicineName), "Наименование", 200),
            UiHelpers.TextColumn(nameof(Batch.BatchNumber), "Партида", 80),
            UiHelpers.TextColumn(nameof(Batch.ExpiryDate), "Срок", 75, "dd.MM.yyyy"),
            UiHelpers.TextColumn(nameof(Batch.Quantity), "Количество", 90, rightAligned: true));

        _expiryGrid.CellFormatting += (_, e) =>
        {
            if (_expiryGrid.Rows[e.RowIndex].DataBoundItem is not Batch batch) return;

            _expiryGrid.Rows[e.RowIndex].DefaultCellStyle.BackColor =
                batch.IsExpired(DateTime.Today) ? UiHelpers.Danger : UiHelpers.Warning;
        };
    }

    /// <summary>Зарежда показателите и предупрежденията.</summary>
    private void LoadData()
    {
        var alerts = AppSession.Stock.GetAlerts(AppSession.CurrentUser!);

        UiHelpers.Bind(_lowStockGrid, alerts.LowStock);
        _lowStockTitle.Text = $"Продукти под минималния запас  ({alerts.LowStock.Count})";

        var expiring = alerts.Expired.Concat(alerts.ExpiringSoon)
            .OrderBy(b => b.ExpiryDate)
            .ToList();

        UiHelpers.Bind(_expiryGrid, expiring);
        _expiryTitle.Text = $"Партиди с изтичащ или изтекъл срок  ({expiring.Count}), " +
                            $"праг {alerts.ExpiryWarningDays} дни";

        BuildTiles(alerts.LowStock.Count, alerts.ExpiringSoon.Count, alerts.Expired.Count);
        _onAlertsChanged();
    }

    /// <summary>Съставя плочките с обобщените показатели за деня.</summary>
    private void BuildTiles(int lowStock, int expiringSoon, int expired)
    {
        _tiles.Controls.Clear();

        var today = DateTime.Today;
        var completed = AppSession.Sales.GetSales(AppSession.CurrentUser!, today, today)
            .Where(s => s.Status == DocumentStatus.Completed)
            .ToList();

        _tiles.Controls.Add(CreateTile("Продажби днес", completed.Count.ToString(),
            "документа", UiHelpers.Primary));

        if (AppSession.IsManager)
        {
            _tiles.Controls.Add(CreateTile("Оборот днес",
                completed.Sum(s => s.TotalAmount).ToString("N2", UiHelpers.Culture),
                Money.CurrencyName, Color.FromArgb(21, 101, 192)));

            _tiles.Controls.Add(CreateTile("Печалба днес",
                completed.Sum(s => s.Profit).ToString("N2", UiHelpers.Culture),
                Money.CurrencyName, Color.FromArgb(46, 125, 50)));
        }

        _tiles.Controls.Add(CreateTile("Ниска наличност", lowStock.ToString(),
            "продукта", lowStock > 0 ? Color.FromArgb(230, 145, 56) : UiHelpers.Muted));

        _tiles.Controls.Add(CreateTile("Изтичащ срок", expiringSoon.ToString(),
            "партиди", expiringSoon > 0 ? Color.FromArgb(230, 145, 56) : UiHelpers.Muted));

        _tiles.Controls.Add(CreateTile("Изтекъл срок", expired.ToString(),
            "партиди", expired > 0 ? Color.FromArgb(183, 28, 28) : UiHelpers.Muted));
    }

    private static Control CreateTile(string caption, string value, string unit, Color color)
    {
        var tile = new TableLayoutPanel
        {
            ColumnCount = 1,
            RowCount = 3,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            MinimumSize = new Size(178, 92),
            BackColor = Color.White,
            Margin = new Padding(0, 0, 12, 0),
            Padding = new Padding(14, 10, 14, 10)
        };

        tile.Paint += (s, e) =>
        {
            var control = (Control)s!;
            using var pen = new Pen(UiHelpers.Border);
            e.Graphics.DrawRectangle(pen, 0, 0, control.Width - 1, control.Height - 1);
            using var accent = new SolidBrush(color);
            e.Graphics.FillRectangle(accent, 0, 0, 3, control.Height);
        };

        tile.Controls.Add(new Label
        {
            Text = caption,
            ForeColor = UiHelpers.Muted,
            Font = UiHelpers.FontSmall,
            AutoSize = true,
            Margin = new Padding(0)
        });

        tile.Controls.Add(new Label
        {
            Text = value,
            ForeColor = color,
            Font = new Font("Segoe UI Semibold", 19f),
            AutoSize = true,
            Margin = new Padding(0, 2, 0, 0)
        });

        tile.Controls.Add(new Label
        {
            Text = unit,
            ForeColor = UiHelpers.Muted,
            Font = UiHelpers.FontSmall,
            AutoSize = true,
            Margin = new Padding(0)
        });

        return tile;
    }

    /// <summary>Съставя протокол за брак на всички партиди с изтекъл срок.</summary>
    private void WriteOffExpired()
    {
        var alerts = AppSession.Stock.GetAlerts(AppSession.CurrentUser!);

        if (alerts.Expired.Count == 0)
        {
            UiHelpers.ShowInfo(this, "Няма партиди с изтекъл срок на годност.");
            return;
        }

        var question = $"Ще бъдат бракувани {alerts.Expired.Count} партиди " +
                       $"с общо {alerts.Expired.Sum(b => b.Quantity)} бр.";

        // Отчетната стойност е финансов показател и се показва само на роли
        // с достъп до него.
        if (AppSession.IsManager)
        {
            var total = alerts.Expired.Sum(b => b.Quantity * b.PurchasePrice);
            question += $" на отчетна стойност {total:N2} {Money.Currency}";
        }

        if (!UiHelpers.Confirm(this, question + ".\n\nЖелаете ли да продължите?"))
            return;

        var writeOff = AppSession.Stock.WriteOffAllExpired(AppSession.CurrentUser!);

        if (writeOff is not null)
        {
            UiHelpers.ShowInfo(this, AppSession.IsManager
                ? $"Съставен е протокол за брак № {writeOff.DocNumber} " +
                  $"на стойност {writeOff.TotalCost:N2} {Money.Currency}"
                : $"Съставен е протокол за брак № {writeOff.DocNumber}.");
        }

        LoadData();
    }
}
