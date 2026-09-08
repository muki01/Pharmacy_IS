using PharmacyIS.Domain;
using PharmacyIS.Domain.Entities;
using PharmacyIS.UI.Infrastructure;

namespace PharmacyIS.UI.Forms;

/// <summary>Показва всички партиди на един лекарствен продукт.</summary>
public class BatchesForm : Form
{
    public BatchesForm(Medicine medicine)
    {
        UiHelpers.StyleDialog(this, $"Партиди: {medicine.Name}", 800, 520);
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = true;

        var grid = new DataGridView { Dock = DockStyle.Fill };
        UiHelpers.StyleGrid(grid);
        grid.Columns.AddRange(
            UiHelpers.TextColumn(nameof(Batch.BatchNumber), "Партиден номер", 120),
            UiHelpers.TextColumn(nameof(Batch.ExpiryDate), "Срок на годност", 110, "dd.MM.yyyy"),
            UiHelpers.TextColumn(nameof(Batch.Quantity), "Налично", 80, rightAligned: true),
            UiHelpers.TextColumn(nameof(Batch.CreatedAt), "Създадена на", 120, "dd.MM.yyyy"));

        var showFinancials = AppSession.IsManager;

        if (showFinancials)
        {
            grid.Columns.Insert(3, UiHelpers.TextColumn(
                nameof(Batch.PurchasePrice), "Доставна цена", 100, "N2", rightAligned: true));
        }

        grid.CellFormatting += (_, e) =>
        {
            if (grid.Rows[e.RowIndex].DataBoundItem is not Batch batch) return;

            grid.Rows[e.RowIndex].DefaultCellStyle.BackColor = batch.DaysToExpiry(DateTime.Today) switch
            {
                < 0 => UiHelpers.Danger,
                <= 60 => UiHelpers.Warning,
                _ => batch.Quantity == 0 ? Color.FromArgb(245, 245, 245) : Color.White
            };
        };

        var batches = AppSession.Stock.GetBatches(AppSession.CurrentUser!, medicine.MedicineId);
        UiHelpers.Bind(grid, batches);

        var summary = new Label
        {
            Dock = DockStyle.Bottom,
            Height = 30,
            Font = UiHelpers.FontBody,
            ForeColor = UiHelpers.Muted,
            Padding = new Padding(2, 6, 0, 0),
            Text = $"Общо налично: {batches.Sum(b => b.Quantity)} бр. · " +
                   $"Брой партиди: {batches.Count}" +
                   (showFinancials
                       ? $" · Отчетна стойност: {batches.Sum(b => b.Quantity * b.PurchasePrice):N2} {Money.Currency}"
                       : string.Empty)
        };

        var content = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12) };
        content.Controls.Add(grid);
        content.Controls.Add(summary);

        Controls.Add(content);
        Controls.Add(UiHelpers.CreateHeader($"Партиди на „{medicine.Name}“",
            $"Код {medicine.Code} · {medicine.FormName} · {medicine.IngredientName}"));
    }
}

/// <summary>Показва движенията по една партида – пълната ѝ история.</summary>
public class BatchMovementsForm : Form
{
    public BatchMovementsForm(Batch batch)
    {
        UiHelpers.StyleDialog(this, $"Движения по партида {batch.BatchNumber}", 900, 560);
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = true;

        var grid = new DataGridView { Dock = DockStyle.Fill };
        UiHelpers.StyleGrid(grid);
        grid.Columns.AddRange(
            UiHelpers.TextColumn(nameof(StockMovement.MovementDate), "Дата и час", 120, "dd.MM.yyyy HH:mm"),
            UiHelpers.TextColumn(nameof(StockMovement.MovementTypeName), "Вид", 80),
            UiHelpers.TextColumn(nameof(StockMovement.Quantity), "Количество", 80, rightAligned: true),
            UiHelpers.TextColumn(nameof(StockMovement.DocType), "Документ", 110),
            UiHelpers.TextColumn(nameof(StockMovement.DocNumber), "Номер", 130),
            UiHelpers.TextColumn(nameof(StockMovement.UserName), "Служител", 150));

        grid.CellFormatting += (_, e) =>
        {
            if (grid.Rows[e.RowIndex].DataBoundItem is not StockMovement movement) return;

            grid.Rows[e.RowIndex].DefaultCellStyle.BackColor = movement.Quantity > 0
                ? UiHelpers.Success
                : Color.White;
        };

        var movements = AppSession.Stock.GetBatchMovements(batch.BatchId);
        UiHelpers.Bind(grid, movements);

        var summary = new Label
        {
            Dock = DockStyle.Bottom,
            Height = 30,
            Font = UiHelpers.FontBody,
            ForeColor = UiHelpers.Muted,
            Padding = new Padding(2, 6, 0, 0),
            Text = $"Сума от движенията: {movements.Sum(m => m.Quantity)} бр. · " +
                   $"Текуща наличност по партидата: {batch.Quantity} бр." +
                   (movements.Sum(m => m.Quantity) == batch.Quantity
                       ? "  ✔ съответствието е потвърдено"
                       : "  ✖ установено е несъответствие")
        };

        var content = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12) };
        content.Controls.Add(grid);
        content.Controls.Add(summary);

        Controls.Add(content);
        Controls.Add(UiHelpers.CreateHeader($"История на партида {batch.BatchNumber}",
            $"{batch.MedicineName} · срок на годност {batch.ExpiryDate:dd.MM.yyyy}"));
    }
}

/// <summary>Регистър на всички складови движения за избран период.</summary>
public class MovementsForm : BaseChildForm
{
    private readonly DateTimePicker _from = new();
    private readonly DateTimePicker _to = new();
    private readonly DataGridView _grid = new();
    private readonly Label _summary = UiHelpers.SummaryLabel();

    public MovementsForm()
        : base("Складови движения", "Пълен журнал на приходите, разходите и браковете")
    {
        AddToolbarButton("Изнеси в CSV", () => UiHelpers.ExportGridToCsv(this, _grid, "Движения"), 150);
        AddToolbarButton("Обнови", LoadData, 110);

        BuildLayout();
        Load += (_, _) => Run(LoadData);
    }

    public override void OnPageActivated() => Run(LoadData);

    private void BuildLayout()
    {
        // Смяната на периода веднага показва движенията за него.
        UiHelpers.SetupPeriod(_from, _to, daysBack: 30, () => Run(LoadData));

        var top = UiHelpers.CreateFilterBar();
        top.Controls.Add(UiHelpers.LabeledField("От дата", _from, 140));
        top.Controls.Add(UiHelpers.LabeledField("До дата", _to, 140));

        UiHelpers.StyleGrid(_grid);
        _grid.Dock = DockStyle.Fill;
        _grid.Columns.AddRange(
            UiHelpers.TextColumn(nameof(StockMovement.MovementDate), "Дата и час", 110, "dd.MM.yyyy HH:mm"),
            UiHelpers.TextColumn(nameof(StockMovement.MedicineName), "Продукт", 200),
            UiHelpers.TextColumn(nameof(StockMovement.BatchNumber), "Партида", 80),
            UiHelpers.TextColumn(nameof(StockMovement.MovementTypeName), "Вид", 70),
            UiHelpers.TextColumn(nameof(StockMovement.Quantity), "Количество", 70, rightAligned: true),
            UiHelpers.TextColumn(nameof(StockMovement.DocNumber), "Документ", 120),
            UiHelpers.TextColumn(nameof(StockMovement.UserName), "Служител", 130));

        _grid.CellFormatting += (_, e) =>
        {
            if (_grid.Rows[e.RowIndex].DataBoundItem is not StockMovement movement) return;

            _grid.Rows[e.RowIndex].DefaultCellStyle.BackColor = movement.MovementType switch
            {
                Domain.Enums.MovementType.In => UiHelpers.Success,
                Domain.Enums.MovementType.WriteOff => UiHelpers.Danger,
                Domain.Enums.MovementType.Reversal => UiHelpers.Warning,
                _ => Color.White
            };
        };

        Content.Controls.Add(_grid);
        Content.Controls.Add(_summary);
        Content.Controls.Add(top);
    }

    private void LoadData()
    {
        var movements = AppSession.Stock.GetMovements(_from.Value, _to.Value);
        UiHelpers.Bind(_grid, movements);

        _summary.Text =
            $"Записи: {movements.Count} · " +
            $"Приход: {movements.Where(m => m.Quantity > 0).Sum(m => m.Quantity)} бр. · " +
            $"Разход: {Math.Abs(movements.Where(m => m.Quantity < 0).Sum(m => m.Quantity))} бр.";
    }
}
