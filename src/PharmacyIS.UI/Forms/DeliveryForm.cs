using PharmacyIS.Domain;
using System.ComponentModel;
using PharmacyIS.Domain.Entities;
using PharmacyIS.Services.Models;
using PharmacyIS.UI.Infrastructure;

namespace PharmacyIS.UI.Forms;

/// <summary>
/// Страница за заприхождаване на доставка. Всеки ред описва партида –
/// продукт, партиден номер, срок на годност, количество и доставна цена.
/// </summary>
public class DeliveryForm : BaseChildForm
{
    private readonly Action _onStockChanged;

    private readonly ComboBox _supplier = new();
    private readonly TextBox _invoice = new();
    private readonly DateTimePicker _date = new();
    private readonly TextBox _note = new();

    private readonly ComboBox _medicine = new();
    private readonly TextBox _batchNumber = new();
    private readonly DateTimePicker _expiry = new();
    private readonly NumericUpDown _quantity = new();
    private readonly NumericUpDown _price = new();

    private readonly DataGridView _grid = new();
    private readonly Label _totalLabel = new();
    private readonly BindingList<Line> _lines = new();

    public DeliveryForm(Action onStockChanged)
        : base("Нова доставка", "Заприхождаване на получени количества по партиди")
    {
        _onStockChanged = onStockChanged;

        AddToolbarButton("Премахни ред", RemoveLine, 150);
        AddToolbarButton("Изчисти всички редове", ClearLines, 180);
        AddToolbarSeparator();
        AddToolbarButton("Заприходи доставката", SaveDelivery, 200, primary: true);

        BuildLayout();
        Load += (_, _) => Run(LoadLookups);
    }

    private void BuildLayout()
    {
        Content.Controls.Add(BuildLinesPanel());
        Content.Controls.Add(BuildLineEditor());
        Content.Controls.Add(UiHelpers.SectionTitle("Ред от доставката"));
        Content.Controls.Add(BuildDocumentHeader());
        Content.Controls.Add(UiHelpers.SectionTitle("Данни за документа"));
    }

    // --- заглавна част на документа -------------------------------------

    private Control BuildDocumentHeader()
    {
        _supplier.DropDownStyle = ComboBoxStyle.DropDownList;

        _invoice.PlaceholderText = "напр. 1024";

        _date.Format = DateTimePickerFormat.Custom;
        _date.CustomFormat = "dd.MM.yyyy";
        _date.Value = DateTime.Today;
        _date.MaxDate = DateTime.Today;

        _note.PlaceholderText = "незадължително";

        var bar = UiHelpers.CreateFilterBar();
        bar.Controls.Add(UiHelpers.LabeledField("Доставчик", _supplier, 280));
        bar.Controls.Add(UiHelpers.LabeledField("Фактура №", _invoice, 140));
        bar.Controls.Add(UiHelpers.LabeledField("Дата на доставка", _date, 150));
        bar.Controls.Add(UiHelpers.LabeledField("Забележка", _note, 260));
        return bar;
    }

    // --- ред от доставката ------------------------------------------------

    private Control BuildLineEditor()
    {
        _medicine.DropDownStyle = ComboBoxStyle.DropDown;
        _medicine.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
        _medicine.AutoCompleteSource = AutoCompleteSource.ListItems;

        _batchNumber.PlaceholderText = "напр. 2604-1187";

        _expiry.Format = DateTimePickerFormat.Custom;
        _expiry.CustomFormat = "dd.MM.yyyy";
        _expiry.Value = DateTime.Today.AddYears(2);
        _expiry.MinDate = DateTime.Today.AddDays(1);

        _quantity.Minimum = 1;
        _quantity.Maximum = 100000;
        _quantity.Value = 10;

        _price.DecimalPlaces = 2;
        _price.Maximum = 100000;
        _price.Increment = 0.10m;

        var add = UiHelpers.CreateButton("Добави ред", 130, primary: true);
        add.Click += (_, _) => Run(AddLine);

        var bar = UiHelpers.CreateFilterBar();
        bar.Controls.Add(UiHelpers.LabeledField("Лекарствен продукт", _medicine, 300));
        bar.Controls.Add(UiHelpers.LabeledField("Партиден номер", _batchNumber, 140));
        bar.Controls.Add(UiHelpers.LabeledField("Срок на годност", _expiry, 150));
        bar.Controls.Add(UiHelpers.LabeledField("Количество", _quantity, 100));
        bar.Controls.Add(UiHelpers.LabeledField("Доставна цена", _price, 110));
        bar.Controls.Add(UiHelpers.BareField(add));
        return bar;
    }

    // --- таблица с редовете ------------------------------------------------

    private Control BuildLinesPanel()
    {
        var panel = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };

        UiHelpers.StyleGrid(_grid);
        _grid.Dock = DockStyle.Fill;
        _grid.Columns.AddRange(
            UiHelpers.TextColumn(nameof(Line.MedicineName), "Продукт", 240),
            UiHelpers.TextColumn(nameof(Line.BatchNumber), "Партида", 100),
            UiHelpers.TextColumn(nameof(Line.ExpiryDate), "Срок", 90, "dd.MM.yyyy"),
            UiHelpers.TextColumn(nameof(Line.Quantity), "Количество", 95, rightAligned: true),
            UiHelpers.TextColumn(nameof(Line.UnitPrice), "Ед. цена", 85, "N2", rightAligned: true),
            UiHelpers.TextColumn(nameof(Line.LineTotal), "Стойност", 95, "N2", rightAligned: true));

        _grid.DataSource = _lines;
        _lines.ListChanged += (_, _) => UpdateTotal();

        _totalLabel.Dock = DockStyle.Bottom;
        _totalLabel.Height = 42;
        _totalLabel.Font = new Font("Segoe UI Semibold", 13f);
        _totalLabel.ForeColor = UiHelpers.Primary;
        _totalLabel.TextAlign = ContentAlignment.MiddleRight;
        _totalLabel.Padding = new Padding(0, 8, 4, 0);
        UpdateTotal();

        panel.Controls.Add(_grid);
        panel.Controls.Add(_totalLabel);
        panel.Controls.Add(UiHelpers.SectionTitle("Позиции на доставката"));
        return panel;
    }

    private void LoadLookups()
    {
        _supplier.DisplayMember = nameof(Supplier.Name);
        _supplier.ValueMember = nameof(Supplier.SupplierId);
        _supplier.DataSource = AppSession.Catalog.GetSuppliers(onlyActive: true);

        _medicine.DisplayMember = nameof(Medicine.Name);
        _medicine.ValueMember = nameof(Medicine.MedicineId);
        _medicine.DataSource = AppSession.Catalog.GetMedicines();
        _medicine.SelectedIndex = -1;
    }

    private void AddLine()
    {
        if (_medicine.SelectedItem is not Medicine medicine)
        {
            UiHelpers.ShowWarning(this, "Изберете лекарствен продукт.");
            _medicine.Focus();
            return;
        }

        if (string.IsNullOrWhiteSpace(_batchNumber.Text))
        {
            UiHelpers.ShowWarning(this, "Въведете партиден номер.");
            _batchNumber.Focus();
            return;
        }

        if (_expiry.Value.Date <= _date.Value.Date)
        {
            UiHelpers.ShowWarning(this,
                "Срокът на годност трябва да е след датата на доставката.");
            _expiry.Focus();
            return;
        }

        if (_price.Value <= 0)
        {
            UiHelpers.ShowWarning(this,
                "Въведете доставната цена от фактурата на доставчика.");
            _price.Focus();
            return;
        }

        _lines.Add(new Line
        {
            MedicineId = medicine.MedicineId,
            MedicineName = medicine.Name,
            BatchNumber = _batchNumber.Text.Trim(),
            ExpiryDate = _expiry.Value.Date,
            Quantity = (int)_quantity.Value,
            UnitPrice = _price.Value
        });

        _batchNumber.Clear();
        _quantity.Value = 10;
        _price.Value = 0;
        _medicine.SelectedIndex = -1;
        _medicine.Focus();
    }

    private void RemoveLine()
    {
        if (_grid.CurrentRow?.DataBoundItem is Line line)
            _lines.Remove(line);
        else
            UiHelpers.ShowWarning(this, "Изберете ред от таблицата.");
    }

    private void ClearLines()
    {
        if (_lines.Count > 0 && UiHelpers.Confirm(this, "Да се изчистят ли всички редове?"))
            _lines.Clear();
    }

    private void UpdateTotal()
    {
        var total = _lines.Sum(l => l.LineTotal);
        _totalLabel.Text = $"Стойност на доставката: {total.ToString("N2", UiHelpers.Culture)} {Money.Currency}";
    }

    private void SaveDelivery()
    {
        if (_lines.Count == 0)
        {
            UiHelpers.ShowWarning(this, "Доставката не съдържа нито един ред.");
            return;
        }

        if (_supplier.SelectedItem is not Supplier supplier)
        {
            UiHelpers.ShowWarning(this, "Изберете доставчик.");
            return;
        }

        var request = new DeliveryRequest
        {
            SupplierId = supplier.SupplierId,
            UserId = AppSession.CurrentUser!.UserId,
            DeliveryDate = _date.Value.Date.Add(DateTime.Now.TimeOfDay),
            InvoiceNumber = string.IsNullOrWhiteSpace(_invoice.Text) ? null : _invoice.Text.Trim(),
            Note = string.IsNullOrWhiteSpace(_note.Text) ? null : _note.Text.Trim(),
            Lines = _lines.Select(l => new DeliveryRequestLine
            {
                MedicineId = l.MedicineId,
                BatchNumber = l.BatchNumber,
                ExpiryDate = l.ExpiryDate,
                Quantity = l.Quantity,
                UnitPrice = l.UnitPrice
            }).ToList()
        };

        var delivery = AppSession.Deliveries.RegisterDelivery(request);

        UiHelpers.ShowInfo(this,
            "Доставката е заприходена.\n\n" +
            $"Документ № {delivery.DocNumber}\n" +
            $"Доставчик: {supplier.Name}\n" +
            $"Позиции: {delivery.Items.Count}\n" +
            $"Стойност: {delivery.TotalAmount:N2} {Money.Currency}",
            "Успешно заприхождаване");

        _lines.Clear();
        _invoice.Clear();
        _note.Clear();
        _onStockChanged();
    }

    /// <summary>Ред от подготвяната доставка.</summary>
    private class Line
    {
        public int MedicineId { get; set; }
        public string MedicineName { get; set; } = string.Empty;
        public string BatchNumber { get; set; } = string.Empty;
        public DateTime ExpiryDate { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }

        public decimal LineTotal => Math.Round(Quantity * UnitPrice, 2, MidpointRounding.AwayFromZero);
    }
}
