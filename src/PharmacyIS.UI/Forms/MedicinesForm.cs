using PharmacyIS.Domain;
using PharmacyIS.Domain.Entities;
using PharmacyIS.UI.Infrastructure;

namespace PharmacyIS.UI.Forms;

/// <summary>
/// Поддържане на каталога на лекарствените продукти: преглед, търсене по
/// няколко критерия, добавяне, редактиране и премахване.
/// </summary>
public class MedicinesForm : BaseChildForm
{
    private readonly TextBox _search = new();
    private readonly ComboBox _ingredientFilter = new();
    private readonly ComboBox _formFilter = new();
    private readonly ComboBox _supplierFilter = new();
    private readonly CheckBox _onlyActive = new();
    private readonly DataGridView _grid = new();
    private readonly Label _summary = UiHelpers.SummaryLabel();

    private bool _loading;

    public MedicinesForm()
        : base("Лекарствени продукти", AppSession.IsManager
            ? "Каталог, търсене и поддържане на данните за продуктите"
            : "Каталог и търсене на лекарствени продукти")
    {
        // Поддържането на каталога определя асортимента и цените и е
        // задължение на управителя. Фармацевтът ползва екрана за справка.
        if (AppSession.IsManager)
        {
            AddToolbarButton("Нов продукт", AddMedicine, 140, primary: true);
            AddToolbarButton("Редактирай", EditMedicine, 140);
            AddToolbarButton("Премахни", RemoveMedicine, 130);
            AddToolbarSeparator();
        }

        AddToolbarButton("Партиди на продукта", ShowBatches, 170, primary: !AppSession.IsManager);
        AddToolbarButton("Изнеси в CSV", () => UiHelpers.ExportGridToCsv(this, _grid, "Каталог"), 150);
        AddToolbarSeparator();
        AddToolbarButton("Обнови", LoadData, 110);

        BuildLayout();
        Load += (_, _) => Run(Initialize);
    }

    public override void OnPageActivated()
    {
        if (!_loading) Run(LoadData);
    }

    private void BuildLayout()
    {
        _search.PlaceholderText = "наименование, код, баркод, съставка…";
        _search.TextChanged += (_, _) => Run(LoadData);

        _ingredientFilter.DropDownStyle = ComboBoxStyle.DropDownList;
        _ingredientFilter.SelectedIndexChanged += (_, _) => Run(LoadData);

        _formFilter.DropDownStyle = ComboBoxStyle.DropDownList;
        _formFilter.SelectedIndexChanged += (_, _) => Run(LoadData);

        _supplierFilter.DropDownStyle = ComboBoxStyle.DropDownList;
        _supplierFilter.SelectedIndexChanged += (_, _) => Run(LoadData);

        _onlyActive.Text = "Само активни";
        _onlyActive.AutoSize = true;
        _onlyActive.Checked = true;
        _onlyActive.CheckedChanged += (_, _) => Run(LoadData);

        var filters = UiHelpers.CreateFilterBar();
        filters.Controls.Add(UiHelpers.LabeledField("Търсене", _search, 300));
        filters.Controls.Add(UiHelpers.LabeledField("Активна съставка", _ingredientFilter, 200));
        filters.Controls.Add(UiHelpers.LabeledField("Лекарствена форма", _formFilter, 170));
        filters.Controls.Add(UiHelpers.LabeledField("Доставчик", _supplierFilter, 220));
        filters.Controls.Add(UiHelpers.BareField(_onlyActive));

        UiHelpers.StyleGrid(_grid);
        _grid.Dock = DockStyle.Fill;
        _grid.Columns.AddRange(
            UiHelpers.TextColumn(nameof(Medicine.Code), "Код", 60),
            UiHelpers.TextColumn(nameof(Medicine.Name), "Наименование", 200),
            UiHelpers.TextColumn(nameof(Medicine.IngredientName), "Активна съставка", 130),
            UiHelpers.TextColumn(nameof(Medicine.FormName), "Форма", 80),
            UiHelpers.TextColumn(nameof(Medicine.Strength), "Дозировка", 80),
            UiHelpers.TextColumn(nameof(Medicine.Manufacturer), "Производител", 100),
            UiHelpers.TextColumn(nameof(Medicine.Price), "Цена", 65, "N2", rightAligned: true),
            UiHelpers.TextColumn(nameof(Medicine.StockQuantity), "Налично", 75, rightAligned: true),
            UiHelpers.TextColumn(nameof(Medicine.MinStock), "Минимум", 78, rightAligned: true),
            UiHelpers.TextColumn(nameof(Medicine.NearestExpiry), "Срок", 78, "dd.MM.yyyy"));

        _grid.CellDoubleClick += (_, e) =>
        {
            if (e.RowIndex < 0) return;
            Run(AppSession.IsManager ? EditMedicine : ShowBatches);
        };
        _grid.CellFormatting += OnCellFormatting;

        Content.Controls.Add(_grid);
        Content.Controls.Add(_summary);
        Content.Controls.Add(filters);
    }

    private void Initialize()
    {
        _loading = true;
        try
        {
            FillFilter(_ingredientFilter, "Всички съставки",
                AppSession.Catalog.GetActiveIngredients().Select(i => (i.IngredientId, i.Name)));

            FillFilter(_formFilter, "Всички форми",
                AppSession.Catalog.GetDosageForms().Select(f => (f.FormId, f.Name)));

            FillFilter(_supplierFilter, "Всички доставчици",
                AppSession.Catalog.GetSuppliers().Select(s => (s.SupplierId, s.Name)));
        }
        finally
        {
            _loading = false;
        }

        LoadData();
    }

    private static void FillFilter(ComboBox combo, string emptyText,
        IEnumerable<(int Id, string Name)> items)
    {
        combo.DisplayMember = "Name";
        combo.ValueMember = "Id";
        combo.DataSource = new[] { (Id: 0, Name: emptyText) }
            .Concat(items)
            .Select(x => new { x.Id, x.Name })
            .ToList();
    }

    private void LoadData()
    {
        if (_loading) return;

        var medicines = AppSession.Catalog.SearchMedicines(
            _search.Text,
            GetSelectedId(_ingredientFilter),
            GetSelectedId(_formFilter),
            GetSelectedId(_supplierFilter),
            _onlyActive.Checked);

        UiHelpers.Bind(_grid, medicines);
        _summary.Text = $"Показани продукти: {medicines.Count} · " +
                        $"с наличност: {medicines.Count(m => m.StockQuantity > 0)} · " +
                        $"под минимума: {medicines.Count(m => m.StockQuantity <= m.MinStock)}" +
                        (AppSession.IsManager
                            ? $" · обща стойност по продажни цени: " +
                              $"{medicines.Sum(m => m.Price * m.StockQuantity):N2} {Money.Currency}"
                            : string.Empty);
    }

    private static int? GetSelectedId(ComboBox combo)
    {
        if (combo.SelectedItem is null) return null;

        var id = (int)(combo.SelectedItem.GetType().GetProperty("Id")?.GetValue(combo.SelectedItem) ?? 0);
        return id > 0 ? id : null;
    }

    private void OnCellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (_grid.Rows[e.RowIndex].DataBoundItem is not Medicine medicine) return;

        var style = _grid.Rows[e.RowIndex].DefaultCellStyle;

        if (!medicine.IsActive)
        {
            style.ForeColor = UiHelpers.Muted;
            style.BackColor = Color.FromArgb(246, 247, 247);
        }
        else if (medicine.StockQuantity == 0)
        {
            style.BackColor = UiHelpers.Danger;
        }
        else if (medicine.StockQuantity <= medicine.MinStock)
        {
            style.BackColor = UiHelpers.Warning;
        }
    }

    private Medicine? Selected => _grid.CurrentRow?.DataBoundItem as Medicine;

    private void AddMedicine()
    {
        using var dialog = new MedicineEditForm(null);
        if (dialog.ShowDialog(this) == DialogResult.OK)
            LoadData();
    }

    private void EditMedicine()
    {
        if (Selected is null)
        {
            UiHelpers.ShowWarning(this, "Изберете продукт от списъка.");
            return;
        }

        using var dialog = new MedicineEditForm(Selected);
        if (dialog.ShowDialog(this) == DialogResult.OK)
            LoadData();
    }

    private void RemoveMedicine()
    {
        if (Selected is null)
        {
            UiHelpers.ShowWarning(this, "Изберете продукт от списъка.");
            return;
        }

        if (!UiHelpers.Confirm(this,
                $"Да се премахне ли продуктът „{Selected.Name}“ от каталога?\n\n" +
                "Ако за продукта има складови документи, той няма да бъде изтрит, " +
                "а само спрян от продажба, за да се запази историята."))
        {
            return;
        }

        var deleted = AppSession.Catalog.RemoveMedicine(AppSession.CurrentUser!, Selected.MedicineId);

        UiHelpers.ShowInfo(this, deleted
            ? "Продуктът е изтрит от каталога."
            : "Продуктът е спрян от продажба. Историята му е запазена.");

        LoadData();
    }

    private void ShowBatches()
    {
        if (Selected is null)
        {
            UiHelpers.ShowWarning(this, "Изберете продукт от списъка.");
            return;
        }

        using var dialog = new BatchesForm(Selected);
        dialog.ShowDialog(this);
    }
}
