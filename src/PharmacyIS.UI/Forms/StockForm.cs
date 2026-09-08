using PharmacyIS.Domain;
using PharmacyIS.Domain.Entities;
using PharmacyIS.UI.Infrastructure;

namespace PharmacyIS.UI.Forms;

/// <summary>
/// Страница „Склад“. Показва наличните партиди, откроява проблемните
/// и позволява бракуване на негодни количества.
///
/// Съгласно заданието при отваряне на страницата се извежда обобщение
/// на предупрежденията за ниска наличност и изтичащи срокове.
/// </summary>
public class StockForm : BaseChildForm
{
    private readonly Action _onStockChanged;

    private readonly TextBox _search = new();
    private readonly ComboBox _filter = new();
    private readonly DataGridView _grid = new();
    private readonly Label _summary = UiHelpers.SummaryLabel();

    private List<Batch> _batches = new();
    private bool _alertsShown;

    /// <summary>Текущата роля има ли достъп до стойностните показатели.</summary>
    private readonly bool _showFinancials = AppSession.IsManager;

    public StockForm(Action onStockChanged)
        : base("Складови наличности", "Партиди, срокове на годност и бракуване")
    {
        _onStockChanged = onStockChanged;

        AddToolbarButton("Бракувай избраната партида", WriteOffSelected, 220);
        AddToolbarButton("Бракувай всички изтекли", WriteOffExpired, 200);
        AddToolbarSeparator();
        AddToolbarButton("Движения по партидата", ShowMovements, 190);
        AddToolbarButton("Изнеси в CSV", () => UiHelpers.ExportGridToCsv(this, _grid, "Наличности"), 150);
        AddToolbarButton("Обнови", LoadData, 110);

        BuildLayout();

        Load += (_, _) => Run(() =>
        {
            LoadData();
            ShowStartupAlerts();
        });
    }

    public override void OnPageActivated()
    {
        Run(() =>
        {
            LoadData();
            ShowStartupAlerts();
        });
    }

    private void BuildLayout()
    {
        _search.PlaceholderText = "продукт или партиден номер…";
        _search.TextChanged += (_, _) => Run(ApplyFilters);

        _filter.DropDownStyle = ComboBoxStyle.DropDownList;
        _filter.Items.AddRange(new object[]
        {
            "Всички налични партиди",
            "Само с изтекъл срок",
            "Само с изтичащ срок",
            "Само под минималния запас"
        });
        _filter.SelectedIndex = 0;
        _filter.SelectedIndexChanged += (_, _) => Run(ApplyFilters);

        var filters = UiHelpers.CreateFilterBar();
        filters.Controls.Add(UiHelpers.LabeledField("Търсене", _search, 320));
        filters.Controls.Add(UiHelpers.LabeledField("Показвани редове", _filter, 240));

        UiHelpers.StyleGrid(_grid);
        _grid.Dock = DockStyle.Fill;
        _grid.Columns.AddRange(
            UiHelpers.TextColumn(nameof(Batch.MedicineCode), "Код", 60),
            UiHelpers.TextColumn(nameof(Batch.MedicineName), "Наименование", 220),
            UiHelpers.TextColumn(nameof(Batch.BatchNumber), "Партида", 90),
            UiHelpers.TextColumn(nameof(Batch.ExpiryDate), "Срок на годност", 90, "dd.MM.yyyy"),
            UiHelpers.TextColumn(nameof(Batch.Quantity), "Количество", 95, rightAligned: true));

        // Доставната цена е финансова информация: показва се само на роли,
        // които имат достъп до стойностните показатели на аптеката.
        if (_showFinancials)
        {
            _grid.Columns.Add(UiHelpers.TextColumn(
                nameof(Batch.PurchasePrice), "Доставна цена", 105, "N2", rightAligned: true));
        }

        _grid.CellFormatting += OnCellFormatting;

        Content.Controls.Add(_grid);
        Content.Controls.Add(_summary);
        Content.Controls.Add(filters);
    }

    private void OnCellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (_grid.Rows[e.RowIndex].DataBoundItem is not Batch batch) return;

        var days = batch.DaysToExpiry(DateTime.Today);

        _grid.Rows[e.RowIndex].DefaultCellStyle.BackColor = days switch
        {
            < 0 => UiHelpers.Danger,
            <= 60 => UiHelpers.Warning,
            _ => Color.White
        };
    }

    private void LoadData()
    {
        _batches = AppSession.Stock.GetAvailableBatches(AppSession.CurrentUser!);
        ApplyFilters();
    }

    private void ApplyFilters()
    {
        var today = DateTime.Today;
        var warningDays = AppSession.Stock.GetExpiryWarningDays();
        var lowStockCodes = AppSession.Reports
            .GetStockReport(AppSession.CurrentUser!, onlyBelowMinimum: true)
            .Select(r => r.MedicineCode)
            .ToHashSet();

        IEnumerable<Batch> query = _batches;

        query = _filter.SelectedIndex switch
        {
            1 => query.Where(b => b.IsExpired(today)),
            2 => query.Where(b => !b.IsExpired(today) && b.DaysToExpiry(today) <= warningDays),
            3 => query.Where(b => lowStockCodes.Contains(b.MedicineCode)),
            _ => query
        };

        if (!string.IsNullOrWhiteSpace(_search.Text))
        {
            var term = _search.Text.Trim();
            query = query.Where(b =>
                b.MedicineName.Contains(term, StringComparison.CurrentCultureIgnoreCase) ||
                b.MedicineCode.Contains(term, StringComparison.CurrentCultureIgnoreCase) ||
                b.BatchNumber.Contains(term, StringComparison.CurrentCultureIgnoreCase));
        }

        var result = query.OrderBy(b => b.ExpiryDate).ThenBy(b => b.MedicineName).ToList();
        UiHelpers.Bind(_grid, result);

        _summary.Text =
            $"Показани партиди: {result.Count} · " +
            $"общо количество: {result.Sum(b => b.Quantity)} бр. · " +
            (_showFinancials
                ? $"отчетна стойност: {result.Sum(b => b.Quantity * b.PurchasePrice):N2} {Money.Currency} · "
                : string.Empty) +
            $"праг за предупреждение: {warningDays} дни";
    }

    /// <summary>Извежда предупрежденията при отваряне на страницата.</summary>
    private void ShowStartupAlerts()
    {
        // Съобщението се показва веднъж за сесията на страницата, за да не
        // прекъсва работата при всяко връщане към нея.
        if (_alertsShown) return;

        var alerts = AppSession.Stock.GetAlerts(AppSession.CurrentUser!);
        if (!alerts.HasAlerts) return;

        _alertsShown = true;

        var message = "Състояние на склада към днешна дата:\n\n";

        if (alerts.LowStock.Count > 0)
        {
            message += $"• Под минималния запас: {alerts.LowStock.Count} продукта\n";
            message += string.Join("\n", alerts.LowStock.Take(5)
                .Select(r => $"    – {r.MedicineName}: {r.Quantity} бр. (мин. {r.MinStock})"));
            if (alerts.LowStock.Count > 5) message += $"\n    … и още {alerts.LowStock.Count - 5}";
            message += "\n\n";
        }

        if (alerts.ExpiringSoon.Count > 0)
            message += $"• С изтичащ срок в следващите {alerts.ExpiryWarningDays} дни: " +
                       $"{alerts.ExpiringSoon.Count} партиди\n";

        if (alerts.Expired.Count > 0)
            message += $"• С изтекъл срок на годност: {alerts.Expired.Count} партиди – " +
                       "подлежат на бракуване.";

        UiHelpers.ShowWarning(this, message, "Предупреждения за склада");
    }

    private Batch? Selected => _grid.CurrentRow?.DataBoundItem as Batch;

    private void WriteOffSelected()
    {
        if (Selected is null)
        {
            UiHelpers.ShowWarning(this, "Изберете партида от списъка.");
            return;
        }

        using var dialog = new WriteOffDialog(Selected);
        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        var writeOff = AppSession.Stock.RegisterWriteOff(
            AppSession.CurrentUser!,
            dialog.Reason,
            new[] { (Selected.BatchId, dialog.Quantity) });

        UiHelpers.ShowInfo(this, _showFinancials
            ? $"Съставен е протокол за брак № {writeOff.DocNumber} " +
              $"на стойност {writeOff.TotalCost:N2} {Money.Currency}"
            : $"Съставен е протокол за брак № {writeOff.DocNumber} " +
              $"за {dialog.Quantity} бр.");

        LoadData();
        _onStockChanged();
    }

    private void WriteOffExpired()
    {
        var expired = _batches.Where(b => b.IsExpired(DateTime.Today)).ToList();

        if (expired.Count == 0)
        {
            UiHelpers.ShowInfo(this, "Няма партиди с изтекъл срок на годност.");
            return;
        }

        var question = $"Ще бъдат бракувани {expired.Count} партиди с общо " +
                       $"{expired.Sum(b => b.Quantity)} бр.";

        if (_showFinancials)
            question += $" на отчетна стойност {expired.Sum(b => b.Quantity * b.PurchasePrice):N2} {Money.Currency}";

        if (!UiHelpers.Confirm(this, question + "\n\nДа се продължи ли?"))
            return;

        var writeOff = AppSession.Stock.WriteOffAllExpired(AppSession.CurrentUser!);

        if (writeOff is not null)
            UiHelpers.ShowInfo(this, $"Съставен е протокол за брак № {writeOff.DocNumber}.");

        LoadData();
        _onStockChanged();
    }

    private void ShowMovements()
    {
        if (Selected is null)
        {
            UiHelpers.ShowWarning(this, "Изберете партида от списъка.");
            return;
        }

        using var dialog = new BatchMovementsForm(Selected);
        dialog.ShowDialog(this);
    }
}

/// <summary>Диалог за въвеждане на количество и основание при бракуване.</summary>
public class WriteOffDialog : Form
{
    private readonly NumericUpDown _quantity = new();
    private readonly ComboBox _reason = new();

    /// <summary>Количество за бракуване.</summary>
    public int Quantity => (int)_quantity.Value;

    /// <summary>Основание за брак.</summary>
    public string Reason => _reason.Text.Trim();

    public WriteOffDialog(Batch batch)
    {
        UiHelpers.StyleDialog(this, "Бракуване на количество", 500, 430);

        var layout = UiHelpers.DialogBody();

        var info = new Label
        {
            Text = $"Продукт: {batch.MedicineName}\n" +
                   $"Партида: {batch.BatchNumber}\n" +
                   $"Срок на годност: {batch.ExpiryDate:dd.MM.yyyy}\n" +
                   $"Налично количество: {batch.Quantity} бр.",
            Font = UiHelpers.FontBody,
            ForeColor = UiHelpers.Ink,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 16)
        };

        _quantity.Minimum = 1;
        _quantity.Maximum = batch.Quantity;
        _quantity.Value = batch.Quantity;
        _quantity.Width = 140;
        _quantity.Font = UiHelpers.FontInput;
        _quantity.Margin = new Padding(0, 0, 0, 16);

        _reason.Width = 420;
        _reason.Font = UiHelpers.FontInput;
        _reason.Items.AddRange(new object[]
        {
            "Изтекъл срок на годност",
            "Повредена опаковка",
            "Нарушени условия на съхранение",
            "Изтеглен от пазара от производителя"
        });
        _reason.SelectedIndex = batch.IsExpired(DateTime.Today) ? 0 : 1;
        _reason.Margin = new Padding(0, 0, 0, 8);

        layout.Controls.Add(info);
        layout.Controls.Add(Caption("Количество за бракуване"));
        layout.Controls.Add(_quantity);
        layout.Controls.Add(Caption("Основание"));
        layout.Controls.Add(_reason);
        layout.Controls.Add(new Label
        {
            Text = "Изберете основание от списъка или въведете свое.",
            Font = UiHelpers.FontSmall,
            ForeColor = UiHelpers.Muted,
            AutoSize = true,
            Margin = new Padding(0)
        });

        var ok = UiHelpers.CreateButton("Бракувай", 130, primary: true);
        ok.Click += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(Reason))
            {
                UiHelpers.ShowWarning(this, "Въведете основание за брак.");
                _reason.Focus();
                return;
            }

            DialogResult = DialogResult.OK;
            Close();
        };

        var cancel = UiHelpers.CreateButton("Отказ", 120);
        cancel.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };

        // Редът на добавяне определя реда на подреждане: контролата с
        // запълване се добавя първа, а прикрепените към ръбовете – след нея.
        Controls.Add(layout);
        Controls.Add(UiHelpers.DialogButtons(ok, cancel));
        Controls.Add(UiHelpers.CreateHeader("Бракуване на количество",
            "Количеството се изписва от партидата и се съставя протокол"));

        AcceptButton = ok;
        CancelButton = cancel;
    }

    private static Label Caption(string text) => new()
    {
        Text = text,
        AutoSize = true,
        Font = UiHelpers.FontBodyBold,
        ForeColor = UiHelpers.Ink,
        Margin = new Padding(0, 0, 0, 4)
    };
}
