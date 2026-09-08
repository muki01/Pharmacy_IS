using PharmacyIS.Domain;
using System.ComponentModel;
using PharmacyIS.Domain.Entities;
using PharmacyIS.Domain.Enums;
using PharmacyIS.Services.Models;
using PharmacyIS.UI.Infrastructure;

namespace PharmacyIS.UI.Forms;

/// <summary>
/// Касова страница за регистриране на продажба.
///
/// Работният цикъл е сведен до минимален брой действия: въвежда се част от
/// наименованието или се сканира баркод, продуктът се добавя с Enter или
/// двукратно щракване, а сумата се преизчислява автоматично при всяка промяна.
/// </summary>
public class SaleForm : BaseChildForm
{
    private readonly Action _onStockChanged;

    private readonly TextBox _search = new();
    private readonly DataGridView _resultsGrid = new();
    private readonly DataGridView _cartGrid = new();
    private readonly NumericUpDown _quantity = new();
    private readonly ComboBox _paymentType = new();
    private readonly Label _totalLabel = new();
    private readonly Label _hintLabel = new();

    private readonly BindingList<CartLine> _cart = new();
    private List<Medicine> _results = new();

    public SaleForm(Action onStockChanged)
        : base("Продажба", "Търсене на продукт, добавяне в сметката и приключване на продажбата")
    {
        _onStockChanged = onStockChanged;

        AddToolbarButton("Добави в сметката  (Enter)", AddSelected, 190);
        AddToolbarButton("Премахни ред  (Del)", RemoveSelected, 170);
        AddToolbarButton("Изчисти сметката", ClearCart, 160);
        AddToolbarSeparator();
        AddToolbarButton("Плащане  (F9)", CompleteSale, 170, primary: true);

        BuildLayout();

        KeyPreview = true;
        KeyDown += OnKeyDown;
        Load += (_, _) => Run(() => { LoadResults(); _search.Focus(); });
    }

    public override void OnPageActivated()
    {
        Run(LoadResults);
        _search.Focus();
        _search.SelectAll();
    }

    // ------------------------------------------------------------------
    //  Изграждане на страницата
    // ------------------------------------------------------------------

    private void BuildLayout()
    {
        var split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            SplitterWidth = 16,
            BackColor = Color.White
        };

        split.Panel1.Controls.Add(BuildSearchPanel());
        split.Panel2.Controls.Add(BuildCartPanel());
        UiHelpers.KeepSplitRatio(split, 0.55);

        Content.Controls.Add(split);
    }

    private Control BuildSearchPanel()
    {
        var panel = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };

        _search.Font = new Font("Segoe UI", 12f);
        _search.PlaceholderText = "наименование, код, баркод, активна съставка или производител…";
        _search.TextChanged += (_, _) => Run(LoadResults);

        var searchBar = UiHelpers.CreateFilterBar();
        var field = UiHelpers.LabeledField("Търсене на продукт  (F2)", _search, 560);
        searchBar.Controls.Add(field);

        UiHelpers.StyleGrid(_resultsGrid);
        _resultsGrid.Dock = DockStyle.Fill;
        _resultsGrid.Columns.AddRange(
            UiHelpers.TextColumn(nameof(Medicine.Code), "Код", 70),
            UiHelpers.TextColumn(nameof(Medicine.Name), "Наименование", 220),
            UiHelpers.TextColumn(nameof(Medicine.FormName), "Форма", 85),
            UiHelpers.TextColumn(nameof(Medicine.Price), "Цена", 70, "N2", rightAligned: true),
            UiHelpers.TextColumn(nameof(Medicine.StockQuantity), "Налично", 80, rightAligned: true),
            UiHelpers.TextColumn(nameof(Medicine.NearestExpiry), "Срок", 80, "dd.MM.yyyy"));

        _resultsGrid.CellDoubleClick += (_, e) => { if (e.RowIndex >= 0) Run(AddSelected); };
        _resultsGrid.CellFormatting += OnResultsFormatting;

        _hintLabel.Dock = DockStyle.Bottom;
        _hintLabel.Height = 24;
        _hintLabel.ForeColor = UiHelpers.Muted;
        _hintLabel.Font = UiHelpers.FontSmall;
        _hintLabel.Padding = new Padding(2, 5, 0, 0);

        panel.Controls.Add(_resultsGrid);
        panel.Controls.Add(_hintLabel);
        panel.Controls.Add(searchBar);
        return panel;
    }

    private Control BuildCartPanel()
    {
        var panel = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };

        UiHelpers.StyleGrid(_cartGrid);
        _cartGrid.Dock = DockStyle.Fill;
        _cartGrid.ReadOnly = false;
        _cartGrid.Columns.AddRange(
            UiHelpers.TextColumn(nameof(CartLine.Name), "Наименование", 210),
            UiHelpers.TextColumn(nameof(CartLine.UnitPrice), "Ед. цена", 80, "N2", rightAligned: true),
            UiHelpers.TextColumn(nameof(CartLine.Quantity), "Кол.", 60, rightAligned: true),
            UiHelpers.TextColumn(nameof(CartLine.LineTotal), "Стойност", 90, "N2", rightAligned: true));

        // Редактира се само количеството; цената идва от каталога.
        _cartGrid.Columns[0].ReadOnly = true;
        _cartGrid.Columns[1].ReadOnly = true;
        _cartGrid.Columns[3].ReadOnly = true;
        _cartGrid.DataSource = _cart;
        _cartGrid.CellValueChanged += (_, _) => UpdateTotal();
        _cartGrid.CellValidating += OnCartCellValidating;
        _cartGrid.DataError += (_, e) => e.ThrowException = false;

        _cart.ListChanged += (_, _) => UpdateTotal();

        panel.Controls.Add(_cartGrid);
        panel.Controls.Add(BuildCheckoutPanel());
        panel.Controls.Add(UiHelpers.SectionTitle("Сметка на клиента"));
        return panel;
    }

    private Control BuildCheckoutPanel()
    {
        var panel = new Panel
        {
            Dock = DockStyle.Bottom,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = Color.White,
            Padding = new Padding(0, 10, 0, 0)
        };

        _quantity.Minimum = 1;
        _quantity.Maximum = 9999;
        _quantity.Value = 1;

        _paymentType.DropDownStyle = ComboBoxStyle.DropDownList;
        _paymentType.Items.AddRange(new object[] { "В брой", "С банкова карта" });
        _paymentType.SelectedIndex = 0;

        var fields = UiHelpers.CreateFilterBar();
        fields.Padding = new Padding(0, 0, 0, 4);
        fields.Controls.Add(UiHelpers.LabeledField("Количество за добавяне", _quantity, 110));
        fields.Controls.Add(UiHelpers.LabeledField("Начин на плащане", _paymentType, 170));

        _totalLabel.Dock = DockStyle.Bottom;
        _totalLabel.Height = 54;
        _totalLabel.TextAlign = ContentAlignment.MiddleRight;
        _totalLabel.Font = new Font("Segoe UI Semibold", 20f);
        _totalLabel.ForeColor = UiHelpers.Primary;
        _totalLabel.Padding = new Padding(0, 0, 4, 0);
        UpdateTotal();

        panel.Controls.Add(_totalLabel);
        panel.Controls.Add(fields);
        return panel;
    }

    // ------------------------------------------------------------------
    //  Данни и действия
    // ------------------------------------------------------------------

    private void LoadResults()
    {
        _results = AppSession.Catalog.SearchMedicines(_search.Text);
        UiHelpers.Bind(_resultsGrid, _results);

        _hintLabel.Text = _results.Count == 0
            ? "Няма намерени продукти по зададения критерий."
            : $"Намерени продукти: {_results.Count}.  Enter или двукратно щракване добавя избрания продукт в сметката.";
    }

    private void OnResultsFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (_resultsGrid.Rows[e.RowIndex].DataBoundItem is not Medicine medicine) return;

        var style = _resultsGrid.Rows[e.RowIndex].DefaultCellStyle;

        style.BackColor = medicine.StockQuantity switch
        {
            0 => UiHelpers.Danger,
            var q when q <= medicine.MinStock => UiHelpers.Warning,
            _ => Color.White
        };

        // Продуктите по лекарско предписание се открояват шрифтово.
        if (medicine.RequiresPrescription)
            style.Font = new Font(UiHelpers.FontBody, FontStyle.Italic);
    }

    private void AddSelected()
    {
        if (_resultsGrid.CurrentRow?.DataBoundItem is not Medicine medicine)
        {
            UiHelpers.ShowWarning(this, "Изберете продукт от списъка.");
            return;
        }

        var quantity = (int)_quantity.Value;
        var existing = _cart.FirstOrDefault(l => l.MedicineId == medicine.MedicineId);
        var requested = quantity + (existing?.Quantity ?? 0);

        if (requested > medicine.StockQuantity)
        {
            UiHelpers.ShowWarning(this,
                $"Наличността на „{medicine.Name}“ е {medicine.StockQuantity} бр. " +
                $"Заявени са {requested} бр.");
            return;
        }

        if (medicine.RequiresPrescription &&
            !UiHelpers.Confirm(this,
                $"„{medicine.Name}“ се отпуска само по лекарско предписание.\n\n" +
                "Представена ли е рецепта?", "Лекарско предписание"))
        {
            return;
        }

        if (existing is not null)
        {
            existing.Quantity = requested;
            _cartGrid.Refresh();
            UpdateTotal();
        }
        else
        {
            _cart.Add(new CartLine
            {
                MedicineId = medicine.MedicineId,
                Code = medicine.Code,
                Name = medicine.Name,
                UnitPrice = medicine.Price,
                Quantity = quantity
            });
        }

        _quantity.Value = 1;
        _search.SelectAll();
        _search.Focus();
    }

    private void RemoveSelected()
    {
        if (_cartGrid.CurrentRow?.DataBoundItem is CartLine line)
            _cart.Remove(line);
        else
            UiHelpers.ShowWarning(this, "Изберете ред от сметката.");
    }

    private void ClearCart()
    {
        if (_cart.Count == 0) return;

        if (UiHelpers.Confirm(this, "Да се изчисти ли текущата сметка?"))
            _cart.Clear();
    }

    private void UpdateTotal()
    {
        var total = AppSession.Sales.CalculateTotal(_cart.Select(l => (l.UnitPrice, l.Quantity)));
        _totalLabel.Text = $"ОБЩО:  {total.ToString("N2", UiHelpers.Culture)} {Money.Currency}";
    }

    private void OnCartCellValidating(object? sender, DataGridViewCellValidatingEventArgs e)
    {
        if (_cartGrid.Columns[e.ColumnIndex].DataPropertyName != nameof(CartLine.Quantity))
            return;

        if (!int.TryParse(e.FormattedValue?.ToString(), out var quantity) || quantity <= 0)
        {
            UiHelpers.ShowWarning(this, "Количеството трябва да е цяло положително число.");
            e.Cancel = true;
            return;
        }

        // Наличността се проверява още при въвеждането, а не чак при
        // плащането, за да не се стигне до отказ след въведена цяла сметка.
        if (_cartGrid.Rows[e.RowIndex].DataBoundItem is not CartLine line) return;

        var available = AppSession.Catalog.GetMedicine(line.MedicineId)?.StockQuantity ?? 0;

        if (quantity > available)
        {
            UiHelpers.ShowWarning(this,
                $"Наличността на „{line.Name}“ е {available} бр. " +
                $"Заявени са {quantity} бр.");
            e.Cancel = true;
        }
    }

    private void CompleteSale()
    {
        if (_cart.Count == 0)
        {
            UiHelpers.ShowWarning(this, "Сметката е празна.");
            return;
        }

        _cartGrid.EndEdit();

        var request = new SaleRequest
        {
            UserId = AppSession.CurrentUser!.UserId,
            SaleDate = DateTime.Now,
            PaymentType = _paymentType.SelectedIndex == 0 ? PaymentType.Cash : PaymentType.Card,
            Lines = _cart.Select(l => new SaleRequestLine(l.MedicineId, l.Quantity)).ToList()
        };

        var sale = AppSession.Sales.RegisterSale(request);

        _cart.Clear();
        LoadResults();
        _onStockChanged();

        using var receipt = new ReceiptForm(sale);
        receipt.ShowDialog(this);

        _search.Clear();
        _search.Focus();
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.KeyCode)
        {
            case Keys.F2:
                _search.SelectAll();
                _search.Focus();
                e.Handled = true;
                break;

            case Keys.F9:
                Run(CompleteSale);
                e.Handled = true;
                break;

            case Keys.Enter when _search.Focused || _resultsGrid.Focused:
                Run(AddSelected);
                e.Handled = true;
                e.SuppressKeyPress = true;
                break;

            case Keys.Delete when _cartGrid.Focused:
                Run(RemoveSelected);
                e.Handled = true;
                break;
        }
    }

    /// <summary>Ред от текущата сметка на клиента.</summary>
    private class CartLine
    {
        public int MedicineId { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public decimal UnitPrice { get; set; }
        public int Quantity { get; set; }

        public decimal LineTotal => Math.Round(UnitPrice * Quantity, 2, MidpointRounding.AwayFromZero);
    }
}
