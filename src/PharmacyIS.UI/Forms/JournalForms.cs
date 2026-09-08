using PharmacyIS.Domain;
using PharmacyIS.Domain.Entities;
using PharmacyIS.Domain.Enums;
using PharmacyIS.UI.Infrastructure;

namespace PharmacyIS.UI.Forms;

/// <summary>Регистър на продажбите с преглед на редовете на избрания документ.</summary>
public class SalesJournalForm : BaseChildForm
{
    private readonly DateTimePicker _from = new();
    private readonly DateTimePicker _to = new();
    private readonly ComboBox _employee = new();
    private readonly DataGridView _grid = new();
    private readonly DataGridView _itemsGrid = new();
    private readonly Label _summary = UiHelpers.SummaryLabel(emphasised: true);

    private bool _loading;

    public SalesJournalForm()
        : base("Регистър на продажбите", AppSession.IsManager
            ? "Издадени документи и техните позиции"
            : "Вашите документи за избрания период и техните позиции")
    {
        AddToolbarButton("Касова бележка", ShowReceipt, 150, primary: true);

        if (AppSession.IsManager)
            AddToolbarButton("Анулирай продажба", CancelSale, 170);

        AddToolbarSeparator();
        AddToolbarButton("Изнеси в CSV", () => UiHelpers.ExportGridToCsv(this, _grid, "Продажби"), 150);
        AddToolbarButton("Обнови", LoadData, 110);

        BuildLayout();
        Load += (_, _) => Run(Initialize);
    }

    public override void OnPageActivated() => Run(LoadData);

    private void BuildLayout()
    {
        // Промяната на който и да е параметър презарежда данните веднага –
        // регистърът не изисква отделно действие „покажи“.
        UiHelpers.SetupPeriod(_from, _to, daysBack: 7, Reload);

        var top = UiHelpers.CreateFilterBar();
        top.Controls.Add(UiHelpers.LabeledField("От дата", _from, 140));
        top.Controls.Add(UiHelpers.LabeledField("До дата", _to, 140));

        // Изборът на служител има смисъл само за управителя – фармацевтът
        // получава единствено собствените си документи.
        if (AppSession.IsManager)
        {
            _employee.DropDownStyle = ComboBoxStyle.DropDownList;
            top.Controls.Add(UiHelpers.LabeledField("Служител", _employee, 240));
        }

        var split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal,
            SplitterWidth = 10
        };

        UiHelpers.StyleGrid(_grid);
        _grid.Dock = DockStyle.Fill;
        _grid.Columns.AddRange(
            UiHelpers.TextColumn(nameof(Sale.DocNumber), "Документ №", 130),
            UiHelpers.TextColumn(nameof(Sale.SaleDate), "Дата и час", 120, "dd.MM.yyyy HH:mm"),
            UiHelpers.TextColumn(nameof(Sale.PaymentType), "Плащане", 90),
            UiHelpers.TextColumn(nameof(Sale.TotalAmount), $"Сума, {Money.Currency}", 90, "N2", rightAligned: true),
            UiHelpers.TextColumn(nameof(Sale.Status), "Състояние", 90));

        // Колоната със служителя има смисъл само когато списъкът съдържа
        // документи на повече от един служител.
        if (AppSession.IsManager)
        {
            _grid.Columns.Insert(2,
                UiHelpers.TextColumn(nameof(Sale.UserName), "Служител", 170));
        }

        _grid.SelectionChanged += (_, _) => Run(LoadItems);
        _grid.CellFormatting += (_, e) =>
        {
            if (_grid.Rows[e.RowIndex].DataBoundItem is not Sale sale) return;

            if (_grid.Columns[e.ColumnIndex].DataPropertyName == nameof(Sale.Status))
                e.Value = sale.Status == DocumentStatus.Completed ? "Приключена" : "Анулирана";

            if (_grid.Columns[e.ColumnIndex].DataPropertyName == nameof(Sale.PaymentType))
                e.Value = sale.PaymentType == PaymentType.Cash ? "В брой" : "С карта";

            if (sale.Status == DocumentStatus.Cancelled)
            {
                _grid.Rows[e.RowIndex].DefaultCellStyle.BackColor = UiHelpers.Danger;
                _grid.Rows[e.RowIndex].DefaultCellStyle.ForeColor = Color.DimGray;
            }
        };

        UiHelpers.StyleGrid(_itemsGrid);
        _itemsGrid.Dock = DockStyle.Fill;
        _itemsGrid.Columns.AddRange(
            UiHelpers.TextColumn(nameof(SaleItem.MedicineCode), "Код", 70),
            UiHelpers.TextColumn(nameof(SaleItem.MedicineName), "Продукт", 240),
            UiHelpers.TextColumn(nameof(SaleItem.BatchNumber), "Партида", 90),
            UiHelpers.TextColumn(nameof(SaleItem.Quantity), "Кол.", 60, rightAligned: true),
            UiHelpers.TextColumn(nameof(SaleItem.UnitPrice), "Ед. цена", 80, "N2", rightAligned: true),
            UiHelpers.TextColumn(nameof(SaleItem.LineTotal), "Стойност", 90, "N2", rightAligned: true));

        split.Panel1.Controls.Add(_grid);
        split.Panel2.Controls.Add(_itemsGrid);
        split.Panel2.Controls.Add(UiHelpers.SectionTitle("Позиции на избрания документ"));

        UiHelpers.KeepSplitRatio(split, 0.6);

        Content.Controls.Add(split);
        Content.Controls.Add(_summary);
        Content.Controls.Add(top);
    }

    /// <summary>Зарежда списъка със служители и данните за периода.</summary>
    private void Initialize()
    {
        if (AppSession.IsManager)
        {
            _loading = true;
            try
            {
                var users = AppSession.Auth.GetAllUsers(AppSession.CurrentUser!);

                _employee.DisplayMember = "Name";
                _employee.ValueMember = "Id";
                _employee.DataSource = new[] { new { Id = 0, Name = "Всички служители" } }
                    .Concat(users.Select(u => new { Id = u.UserId, Name = u.FullName }))
                    .ToList();

                _employee.SelectedIndexChanged += (_, _) => Reload();
            }
            finally
            {
                _loading = false;
            }
        }

        LoadData();
    }

    /// <summary>Презарежда данните след промяна на параметър.</summary>
    private void Reload()
    {
        if (!_loading) Run(LoadData);
    }

    private void LoadData()
    {
        int? employeeId = null;
        if (AppSession.IsManager && _employee.SelectedItem is not null)
        {
            var id = (int)(_employee.SelectedItem.GetType()
                .GetProperty("Id")?.GetValue(_employee.SelectedItem) ?? 0);

            if (id > 0) employeeId = id;
        }

        var sales = AppSession.Sales.GetSales(
            AppSession.CurrentUser!, _from.Value, _to.Value, employeeId);
        UiHelpers.Bind(_grid, sales);

        var completed = sales.Where(s => s.Status == DocumentStatus.Completed).ToList();

        _summary.Text = AppSession.IsManager
            ? $"Документи: {sales.Count} (приключени {completed.Count}) · " +
              $"Оборот: {completed.Sum(s => s.TotalAmount):N2} {Money.Currency} · " +
              $"Печалба: {completed.Sum(s => s.Profit):N2} {Money.Currency}"
            : $"Документи: {sales.Count} (приключени {completed.Count})";

        LoadItems();
    }

    private void LoadItems()
    {
        if (_grid.CurrentRow?.DataBoundItem is not Sale sale)
        {
            _itemsGrid.DataSource = null;
            return;
        }

        var full = AppSession.Sales.GetSale(AppSession.CurrentUser!, sale.SaleId);
        UiHelpers.Bind(_itemsGrid, full?.Items);
    }

    private void ShowReceipt()
    {
        if (_grid.CurrentRow?.DataBoundItem is not Sale sale)
        {
            UiHelpers.ShowWarning(this, "Изберете документ от списъка.");
            return;
        }

        var full = AppSession.Sales.GetSale(AppSession.CurrentUser!, sale.SaleId);
        if (full is null) return;

        using var receipt = new ReceiptForm(full);
        receipt.ShowDialog(this);
    }

    private void CancelSale()
    {
        if (_grid.CurrentRow?.DataBoundItem is not Sale sale)
        {
            UiHelpers.ShowWarning(this, "Изберете документ от списъка.");
            return;
        }

        if (!UiHelpers.Confirm(this,
                $"Да се анулира ли продажба № {sale.DocNumber} на стойност {sale.TotalAmount:N2} {Money.Currency}?\n\n" +
                "Изписаните количества ще бъдат върнати в съответните партиди."))
        {
            return;
        }

        AppSession.Sales.CancelSale(AppSession.CurrentUser!, sale.SaleId);
        UiHelpers.ShowInfo(this, "Продажбата е анулирана и количествата са възстановени.");
        LoadData();
    }
}

/// <summary>Регистър на доставките с преглед на редовете на избрания документ.</summary>
public class DeliveriesJournalForm : BaseChildForm
{
    private readonly DateTimePicker _from = new();
    private readonly DateTimePicker _to = new();
    private readonly ComboBox _supplier = new();
    private readonly DataGridView _grid = new();
    private readonly DataGridView _itemsGrid = new();
    private readonly Label _summary = UiHelpers.SummaryLabel(emphasised: true);

    public DeliveriesJournalForm()
        : base("Регистър на доставките", "Заприходени документи и техните позиции")
    {
        AddToolbarButton("Изнеси в CSV", () => UiHelpers.ExportGridToCsv(this, _grid, "Доставки"), 150);
        AddToolbarButton("Обнови", LoadData, 110);

        BuildLayout();
        Load += (_, _) => Run(Initialize);
    }

    public override void OnPageActivated() => Run(LoadData);

    private void BuildLayout()
    {
        _supplier.DropDownStyle = ComboBoxStyle.DropDownList;

        UiHelpers.SetupPeriod(_from, _to, daysBack: 30, () => Run(LoadData));

        var top = UiHelpers.CreateFilterBar();
        top.Controls.Add(UiHelpers.LabeledField("От дата", _from, 140));
        top.Controls.Add(UiHelpers.LabeledField("До дата", _to, 140));
        top.Controls.Add(UiHelpers.LabeledField("Доставчик", _supplier, 240));

        var split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal,
            SplitterWidth = 10
        };

        UiHelpers.StyleGrid(_grid);
        _grid.Dock = DockStyle.Fill;
        _grid.Columns.AddRange(
            UiHelpers.TextColumn(nameof(Delivery.DocNumber), "Документ №", 130),
            UiHelpers.TextColumn(nameof(Delivery.DeliveryDate), "Дата", 100, "dd.MM.yyyy"),
            UiHelpers.TextColumn(nameof(Delivery.SupplierName), "Доставчик", 200),
            UiHelpers.TextColumn(nameof(Delivery.InvoiceNumber), "Фактура №", 90),
            UiHelpers.TextColumn(nameof(Delivery.UserName), "Приел", 150),
            UiHelpers.TextColumn(nameof(Delivery.TotalAmount), $"Стойност, {Money.Currency}", 100, "N2", rightAligned: true));

        _grid.SelectionChanged += (_, _) => Run(LoadItems);

        UiHelpers.StyleGrid(_itemsGrid);
        _itemsGrid.Dock = DockStyle.Fill;
        _itemsGrid.Columns.AddRange(
            UiHelpers.TextColumn(nameof(DeliveryItem.MedicineCode), "Код", 70),
            UiHelpers.TextColumn(nameof(DeliveryItem.MedicineName), "Продукт", 240),
            UiHelpers.TextColumn(nameof(DeliveryItem.BatchNumber), "Партида", 90),
            UiHelpers.TextColumn(nameof(DeliveryItem.ExpiryDate), "Срок", 85, "dd.MM.yyyy"),
            UiHelpers.TextColumn(nameof(DeliveryItem.Quantity), "Кол.", 60, rightAligned: true),
            UiHelpers.TextColumn(nameof(DeliveryItem.UnitPrice), "Ед. цена", 80, "N2", rightAligned: true),
            UiHelpers.TextColumn(nameof(DeliveryItem.LineTotal), "Стойност", 90, "N2", rightAligned: true));

        split.Panel1.Controls.Add(_grid);
        split.Panel2.Controls.Add(_itemsGrid);
        split.Panel2.Controls.Add(UiHelpers.SectionTitle("Позиции на избрания документ"));

        UiHelpers.KeepSplitRatio(split, 0.6);

        Content.Controls.Add(split);
        Content.Controls.Add(_summary);
        Content.Controls.Add(top);
    }

    private void Initialize()
    {
        var suppliers = AppSession.Catalog.GetSuppliers();

        _supplier.DisplayMember = "Name";
        _supplier.ValueMember = "Id";
        _supplier.DataSource = new[] { new { Id = 0, Name = "Всички доставчици" } }
            .Concat(suppliers.Select(s => new { Id = s.SupplierId, Name = s.Name }))
            .ToList();

        _supplier.SelectedIndexChanged += (_, _) => Run(LoadData);
        LoadData();
    }

    private void LoadData()
    {
        int? supplierId = null;
        if (_supplier.SelectedItem is not null)
        {
            var id = (int)(_supplier.SelectedItem.GetType()
                .GetProperty("Id")?.GetValue(_supplier.SelectedItem) ?? 0);

            if (id > 0) supplierId = id;
        }

        var deliveries = AppSession.Deliveries.GetDeliveries(
            AppSession.CurrentUser!, _from.Value, _to.Value, supplierId);
        UiHelpers.Bind(_grid, deliveries);

        _summary.Text = $"Документи: {deliveries.Count} · " +
                        $"Обща стойност: {deliveries.Sum(d => d.TotalAmount):N2} {Money.Currency}";

        LoadItems();
    }

    private void LoadItems()
    {
        if (_grid.CurrentRow?.DataBoundItem is not Delivery delivery)
        {
            _itemsGrid.DataSource = null;
            return;
        }

        var full = AppSession.Deliveries.GetDelivery(AppSession.CurrentUser!, delivery.DeliveryId);
        UiHelpers.Bind(_itemsGrid, full?.Items);
    }
}
