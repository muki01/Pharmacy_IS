using PharmacyIS.Domain.Entities;
using PharmacyIS.UI.Infrastructure;

namespace PharmacyIS.UI.Forms;

/// <summary>Поддържане на списъка с доставчици.</summary>
public class SuppliersForm : BaseChildForm
{
    private readonly TextBox _search = new();
    private readonly DataGridView _grid = new();
    private readonly Label _summary = UiHelpers.SummaryLabel();

    public SuppliersForm()
        : base("Доставчици", "Данни за контрагентите, от които аптеката получава доставки")
    {
        AddToolbarButton("Нов доставчик", AddSupplier, 150, primary: true);
        AddToolbarButton("Редактирай", EditSupplier, 140);
        AddToolbarButton("Премахни", RemoveSupplier, 130);
        AddToolbarSeparator();
        AddToolbarButton("Изнеси в CSV", () => UiHelpers.ExportGridToCsv(this, _grid, "Доставчици"), 150);
        AddToolbarButton("Обнови", LoadData, 110);

        BuildLayout();
        Load += (_, _) => Run(LoadData);
    }

    private void BuildLayout()
    {
        _search.PlaceholderText = "наименование, код или ЕИК…";
        _search.TextChanged += (_, _) => Run(LoadData);

        var filters = UiHelpers.CreateFilterBar();
        filters.Controls.Add(UiHelpers.LabeledField("Търсене", _search, 340));

        UiHelpers.StyleGrid(_grid);
        _grid.Dock = DockStyle.Fill;
        _grid.Columns.AddRange(
            UiHelpers.TextColumn(nameof(Supplier.Code), "Код", 50),
            UiHelpers.TextColumn(nameof(Supplier.Name), "Наименование", 180),
            UiHelpers.TextColumn(nameof(Supplier.Bulstat), "ЕИК/БУЛСТАТ", 90),
            UiHelpers.TextColumn(nameof(Supplier.ContactPerson), "Лице за контакт", 130),
            UiHelpers.TextColumn(nameof(Supplier.Phone), "Телефон", 100),
            UiHelpers.TextColumn(nameof(Supplier.Email), "Ел. поща", 150),
            UiHelpers.TextColumn(nameof(Supplier.Address), "Адрес", 200));

        _grid.CellDoubleClick += (_, e) => { if (e.RowIndex >= 0) Run(EditSupplier); };
        _grid.CellFormatting += (_, e) =>
        {
            if (_grid.Rows[e.RowIndex].DataBoundItem is Supplier { IsActive: false })
            {
                _grid.Rows[e.RowIndex].DefaultCellStyle.ForeColor = UiHelpers.Muted;
                _grid.Rows[e.RowIndex].DefaultCellStyle.BackColor = Color.FromArgb(246, 247, 247);
            }
        };

        Content.Controls.Add(_grid);
        Content.Controls.Add(_summary);
        Content.Controls.Add(filters);
    }

    public override void OnPageActivated() => Run(LoadData);

    private void LoadData()
    {
        var suppliers = AppSession.Catalog.SearchSuppliers(_search.Text);
        UiHelpers.Bind(_grid, suppliers);
        _summary.Text = $"Показани доставчици: {suppliers.Count} · " +
                        $"активни: {suppliers.Count(s => s.IsActive)}";
    }

    private Supplier? Selected => _grid.CurrentRow?.DataBoundItem as Supplier;

    private void AddSupplier()
    {
        using var dialog = new SupplierEditForm(null);
        if (dialog.ShowDialog(this) == DialogResult.OK) LoadData();
    }

    private void EditSupplier()
    {
        if (Selected is null)
        {
            UiHelpers.ShowWarning(this, "Изберете доставчик от списъка.");
            return;
        }

        using var dialog = new SupplierEditForm(Selected);
        if (dialog.ShowDialog(this) == DialogResult.OK) LoadData();
    }

    private void RemoveSupplier()
    {
        if (Selected is null)
        {
            UiHelpers.ShowWarning(this, "Изберете доставчик от списъка.");
            return;
        }

        if (!UiHelpers.Confirm(this,
                $"Да се премахне ли доставчикът „{Selected.Name}“?\n\n" +
                "Ако за него има регистрирани доставки, записът само ще бъде деактивиран."))
        {
            return;
        }

        var deleted = AppSession.Catalog.RemoveSupplier(AppSession.CurrentUser!, Selected.SupplierId);

        UiHelpers.ShowInfo(this, deleted
            ? "Доставчикът е изтрит."
            : "Доставчикът е деактивиран. Историята на доставките е запазена.");

        LoadData();
    }
}

/// <summary>Диалог за въвеждане и редактиране на данни за доставчик.</summary>
public class SupplierEditForm : Form
{
    private readonly Supplier _supplier;
    private readonly bool _isNew;

    private readonly TextBox _code = new();
    private readonly TextBox _name = new();
    private readonly TextBox _bulstat = new();
    private readonly TextBox _contact = new();
    private readonly TextBox _phone = new();
    private readonly TextBox _email = new();
    private readonly TextBox _address = new();
    private readonly CheckBox _active = new();

    public SupplierEditForm(Supplier? supplier)
    {
        _isNew = supplier is null;
        _supplier = supplier is null
            ? new Supplier { IsActive = true }
            : new Supplier
            {
                SupplierId = supplier.SupplierId,
                Code = supplier.Code,
                Name = supplier.Name,
                Bulstat = supplier.Bulstat,
                ContactPerson = supplier.ContactPerson,
                Phone = supplier.Phone,
                Email = supplier.Email,
                Address = supplier.Address,
                IsActive = supplier.IsActive
            };

        UiHelpers.StyleDialog(this,
            _isNew ? "Нов доставчик" : $"Редактиране: {_supplier.Name}", 580, 500);

        BuildLayout();
        FillFields();
    }

    private void BuildLayout()
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            Padding = new Padding(24, 18, 24, 10),
            AutoScroll = true,
            BackColor = Color.White
        };

        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        foreach (var box in new[] { _code, _name, _bulstat, _contact, _phone, _email, _address })
        {
            box.Width = 300;
            box.Font = UiHelpers.FontInput;
        }

        _active.Text = "Активен доставчик";
        _active.AutoSize = true;

        AddRow(layout, "Код (4 цифри)", _code);
        AddRow(layout, "Наименование", _name);
        AddRow(layout, "ЕИК/БУЛСТАТ", _bulstat);
        AddRow(layout, "Лице за контакт", _contact);
        AddRow(layout, "Телефон", _phone);
        AddRow(layout, "Електронна поща", _email);
        AddRow(layout, "Адрес", _address);
        AddRow(layout, string.Empty, _active);

        var save = UiHelpers.CreateButton("Запис", 120, primary: true);
        save.Click += OnSave;

        var cancel = UiHelpers.CreateButton("Отказ", 120);
        cancel.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };

        Controls.Add(layout);
        Controls.Add(UiHelpers.DialogButtons(save, cancel));
        Controls.Add(UiHelpers.CreateHeader(
            _isNew ? "Нов доставчик" : "Редактиране на доставчик",
            "Данните се проверяват при запис"));

        AcceptButton = save;
        CancelButton = cancel;
    }

    private static void AddRow(TableLayoutPanel layout, string caption, Control control)
    {
        layout.Controls.Add(new Label
        {
            Text = caption,
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Font = UiHelpers.FontBody,
            ForeColor = UiHelpers.Ink,
            Margin = new Padding(0, 7, 10, 4)
        });

        control.Margin = new Padding(0, 4, 0, 6);
        layout.Controls.Add(control);
    }

    private void FillFields()
    {
        _code.Text = _supplier.Code;
        _name.Text = _supplier.Name;
        _bulstat.Text = _supplier.Bulstat;
        _contact.Text = _supplier.ContactPerson;
        _phone.Text = _supplier.Phone;
        _email.Text = _supplier.Email;
        _address.Text = _supplier.Address;
        _active.Checked = _supplier.IsActive;
    }

    private void OnSave(object? sender, EventArgs e)
    {
        try
        {
            _supplier.Code = _code.Text.Trim();
            _supplier.Name = _name.Text.Trim();
            _supplier.Bulstat = _bulstat.Text.Trim();
            _supplier.ContactPerson = _contact.Text.Trim();
            _supplier.Phone = _phone.Text.Trim();
            _supplier.Email = _email.Text.Trim();
            _supplier.Address = _address.Text.Trim();
            _supplier.IsActive = _active.Checked;

            if (_isNew)
                AppSession.Catalog.AddSupplier(AppSession.CurrentUser!, _supplier);
            else
                AppSession.Catalog.UpdateSupplier(AppSession.CurrentUser!, _supplier);

            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex)
        {
            UiHelpers.ShowError(this, ex);
        }
    }
}
