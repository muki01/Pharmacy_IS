using PharmacyIS.Domain;
using PharmacyIS.Domain.Entities;
using PharmacyIS.UI.Infrastructure;

namespace PharmacyIS.UI.Forms;

/// <summary>
/// Диалог за въвеждане и редактиране на данните за лекарствен продукт.
/// Проверката на въведеното се извършва в слоя с бизнес логика, а
/// съобщенията се показват до съответното поле.
/// </summary>
public class MedicineEditForm : Form
{
    private readonly Medicine _medicine;
    private readonly bool _isNew;

    private readonly TextBox _code = new();
    private readonly TextBox _name = new();
    private readonly ComboBox _ingredient = new();
    private readonly ComboBox _form = new();
    private readonly TextBox _manufacturer = new();
    private readonly TextBox _strength = new();
    private readonly NumericUpDown _price = new();
    private readonly NumericUpDown _minStock = new();
    private readonly TextBox _barcode = new();
    private readonly CheckBox _prescription = new();
    private readonly CheckBox _active = new();

    public MedicineEditForm(Medicine? medicine)
    {
        _isNew = medicine is null;
        _medicine = medicine is null
            ? new Medicine { IsActive = true, MinStock = 10 }
            : Clone(medicine);

        UiHelpers.StyleDialog(this,
            _isNew ? "Нов лекарствен продукт" : $"Редактиране: {_medicine.Name}", 620, 640);

        BuildLayout();
        LoadLookups();
        FillFields();
    }

    /// <summary>
    /// Работи се върху копие, за да не се променя записът в списъка,
    /// ако потребителят се откаже от редакцията.
    /// </summary>
    private static Medicine Clone(Medicine source) => new()
    {
        MedicineId = source.MedicineId,
        Code = source.Code,
        Name = source.Name,
        IngredientId = source.IngredientId,
        FormId = source.FormId,
        Manufacturer = source.Manufacturer,
        Strength = source.Strength,
        Price = source.Price,
        RequiresPrescription = source.RequiresPrescription,
        MinStock = source.MinStock,
        Barcode = source.Barcode,
        IsActive = source.IsActive
    };

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

        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 200));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        foreach (var box in new Control[] { _code, _name, _manufacturer, _strength, _barcode })
            box.Font = UiHelpers.FontInput;

        _code.Width = 300;
        _name.Width = 300;
        _ingredient.Width = 300;
        _ingredient.DropDownStyle = ComboBoxStyle.DropDownList;
        _form.Width = 300;
        _form.DropDownStyle = ComboBoxStyle.DropDownList;
        _manufacturer.Width = 300;
        _strength.Width = 300;
        _barcode.Width = 300;

        _price.Width = 140;
        _price.DecimalPlaces = 2;
        _price.Maximum = 100000;
        _price.Increment = 0.10m;

        _minStock.Width = 140;
        _minStock.Maximum = 100000;

        _prescription.Text = "Отпуска се само по лекарско предписание";
        _prescription.AutoSize = true;

        _active.Text = "Активен (участва в продажби)";
        _active.AutoSize = true;

        AddRow(layout, "Код (8 цифри)", _code);
        AddRow(layout, "Наименование", _name);
        AddRow(layout, "Активна съставка", _ingredient);
        AddRow(layout, "Лекарствена форма", _form);
        AddRow(layout, "Дозировка", _strength);
        AddRow(layout, "Производител", _manufacturer);
        AddRow(layout, $"Продажна цена, {Money.Currency}", _price);
        AddRow(layout, "Минимална наличност", _minStock);
        AddRow(layout, "Баркод", _barcode);
        AddRow(layout, string.Empty, _prescription);
        AddRow(layout, string.Empty, _active);

        var save = UiHelpers.CreateButton("Запис", 120, primary: true);
        save.Click += OnSave;

        var cancel = UiHelpers.CreateButton("Отказ", 120);
        cancel.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };

        Controls.Add(layout);
        Controls.Add(UiHelpers.DialogButtons(save, cancel));
        Controls.Add(UiHelpers.CreateHeader(
            _isNew ? "Нов продукт" : "Редактиране на продукт",
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

    private void LoadLookups()
    {
        _ingredient.DisplayMember = nameof(ActiveIngredient.Name);
        _ingredient.ValueMember = nameof(ActiveIngredient.IngredientId);
        _ingredient.DataSource = AppSession.Catalog.GetActiveIngredients();

        _form.DisplayMember = nameof(DosageForm.Name);
        _form.ValueMember = nameof(DosageForm.FormId);
        _form.DataSource = AppSession.Catalog.GetDosageForms();
    }

    private void FillFields()
    {
        _code.Text = _medicine.Code;
        _name.Text = _medicine.Name;
        _manufacturer.Text = _medicine.Manufacturer;
        _strength.Text = _medicine.Strength;
        _barcode.Text = _medicine.Barcode;
        _price.Value = Math.Clamp(_medicine.Price, 0, _price.Maximum);
        _minStock.Value = Math.Clamp(_medicine.MinStock, 0, (int)_minStock.Maximum);
        _prescription.Checked = _medicine.RequiresPrescription;
        _active.Checked = _medicine.IsActive;

        if (_medicine.IngredientId > 0) _ingredient.SelectedValue = _medicine.IngredientId;
        if (_medicine.FormId > 0) _form.SelectedValue = _medicine.FormId;
    }

    private void OnSave(object? sender, EventArgs e)
    {
        try
        {
            _medicine.Code = _code.Text.Trim();
            _medicine.Name = _name.Text.Trim();
            _medicine.IngredientId = (int)(_ingredient.SelectedValue ?? 0);
            _medicine.FormId = (int)(_form.SelectedValue ?? 0);
            _medicine.Manufacturer = _manufacturer.Text.Trim();
            _medicine.Strength = _strength.Text.Trim();
            _medicine.Price = _price.Value;
            _medicine.MinStock = (int)_minStock.Value;
            _medicine.Barcode = _barcode.Text.Trim();
            _medicine.RequiresPrescription = _prescription.Checked;
            _medicine.IsActive = _active.Checked;

            if (_isNew)
                AppSession.Catalog.AddMedicine(AppSession.CurrentUser!, _medicine);
            else
                AppSession.Catalog.UpdateMedicine(AppSession.CurrentUser!, _medicine);

            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex)
        {
            UiHelpers.ShowError(this, ex);
        }
    }
}
