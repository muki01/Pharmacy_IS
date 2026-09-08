using PharmacyIS.Domain.Entities;
using PharmacyIS.UI.Infrastructure;

namespace PharmacyIS.UI.Forms;

/// <summary>
/// Поддържане на номенклатурните файлове „Активни съставки“ и
/// „Лекарствени форми“, използвани при класификацията на продуктите.
/// </summary>
public class NomenclatureForm : BaseChildForm
{
    private readonly DataGridView _ingredientsGrid = new();
    private readonly DataGridView _formsGrid = new();

    public NomenclatureForm()
        : base("Номенклатури", "Активни съставки и лекарствени форми")
    {
        AddToolbarButton("Нова активна съставка", AddIngredient, 190, primary: true);
        AddToolbarButton("Редактирай съставка", EditIngredient, 180);
        AddToolbarSeparator();
        AddToolbarButton("Нова лекарствена форма", AddForm, 200);
        AddToolbarButton("Редактирай форма", EditForm, 160);
        AddToolbarSeparator();
        AddToolbarButton("Обнови", LoadData, 110);

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

        UiHelpers.StyleGrid(_ingredientsGrid);
        _ingredientsGrid.Dock = DockStyle.Fill;
        _ingredientsGrid.Columns.AddRange(
            UiHelpers.TextColumn(nameof(ActiveIngredient.Code), "Код", 60),
            UiHelpers.TextColumn(nameof(ActiveIngredient.Name), "Наименование", 240));
        _ingredientsGrid.CellDoubleClick += (_, e) => { if (e.RowIndex >= 0) Run(EditIngredient); };

        UiHelpers.StyleGrid(_formsGrid);
        _formsGrid.Dock = DockStyle.Fill;
        _formsGrid.Columns.AddRange(
            UiHelpers.TextColumn(nameof(DosageForm.Code), "Код", 60),
            UiHelpers.TextColumn(nameof(DosageForm.Name), "Наименование", 240));
        _formsGrid.CellDoubleClick += (_, e) => { if (e.RowIndex >= 0) Run(EditForm); };

        split.Panel1.Controls.Add(_ingredientsGrid);
        split.Panel1.Controls.Add(UiHelpers.SectionTitle("Активни съставки (INN)"));
        split.Panel2.Controls.Add(_formsGrid);
        split.Panel2.Controls.Add(UiHelpers.SectionTitle("Лекарствени форми"));

        UiHelpers.KeepSplitRatio(split, 0.5);
        Content.Controls.Add(split);
    }

    private void LoadData()
    {
        UiHelpers.Bind(_ingredientsGrid, AppSession.Catalog.GetActiveIngredients());
        UiHelpers.Bind(_formsGrid, AppSession.Catalog.GetDosageForms());
    }

    private void AddIngredient()
    {
        using var dialog = new NomenclatureEditDialog("Нова активна съставка", "Код (3 цифри)", string.Empty, string.Empty);
        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        AppSession.Catalog.AddIngredient(AppSession.CurrentUser!,
            new ActiveIngredient { Code = dialog.Code, Name = dialog.ItemName });
        LoadData();
    }

    private void EditIngredient()
    {
        if (_ingredientsGrid.CurrentRow?.DataBoundItem is not ActiveIngredient ingredient)
        {
            UiHelpers.ShowWarning(this, "Изберете активна съставка от списъка.");
            return;
        }

        using var dialog = new NomenclatureEditDialog("Редактиране на активна съставка",
            "Код (3 цифри)", ingredient.Code, ingredient.Name);

        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        AppSession.Catalog.UpdateIngredient(AppSession.CurrentUser!, new ActiveIngredient
        {
            IngredientId = ingredient.IngredientId,
            Code = dialog.Code,
            Name = dialog.ItemName
        });

        LoadData();
    }

    private void AddForm()
    {
        using var dialog = new NomenclatureEditDialog("Нова лекарствена форма", "Код (2 цифри)", string.Empty, string.Empty);
        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        AppSession.Catalog.AddDosageForm(AppSession.CurrentUser!,
            new DosageForm { Code = dialog.Code, Name = dialog.ItemName });
        LoadData();
    }

    private void EditForm()
    {
        if (_formsGrid.CurrentRow?.DataBoundItem is not DosageForm form)
        {
            UiHelpers.ShowWarning(this, "Изберете лекарствена форма от списъка.");
            return;
        }

        using var dialog = new NomenclatureEditDialog("Редактиране на лекарствена форма",
            "Код (2 цифри)", form.Code, form.Name);

        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        AppSession.Catalog.UpdateDosageForm(AppSession.CurrentUser!, new DosageForm
        {
            FormId = form.FormId,
            Code = dialog.Code,
            Name = dialog.ItemName
        });

        LoadData();
    }
}

/// <summary>Общ диалог за въвеждане на код и наименование на номенклатурна позиция.</summary>
public class NomenclatureEditDialog : Form
{
    private readonly TextBox _code = new();
    private readonly TextBox _name = new();

    public string Code => _code.Text.Trim();
    public string ItemName => _name.Text.Trim();

    public NomenclatureEditDialog(string title, string codeCaption, string code, string name)
    {
        UiHelpers.StyleDialog(this, title, 460, 300);

        _code.Text = code;
        _code.Width = 120;
        _code.Font = UiHelpers.FontInput;

        _name.Text = name;
        _name.Width = 360;
        _name.Font = UiHelpers.FontInput;

        var layout = UiHelpers.DialogBody();

        layout.Controls.Add(new Label
        {
            Text = codeCaption, AutoSize = true, Font = UiHelpers.FontBodyBold,
            ForeColor = UiHelpers.Ink, Margin = new Padding(0, 0, 0, 4)
        });
        layout.Controls.Add(_code);
        layout.Controls.Add(new Label
        {
            Text = "Наименование", AutoSize = true, Font = UiHelpers.FontBodyBold,
            ForeColor = UiHelpers.Ink, Margin = new Padding(0, 12, 0, 4)
        });
        layout.Controls.Add(_name);

        var ok = UiHelpers.CreateButton("Запис", 110, primary: true);
        ok.Click += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(Code) || string.IsNullOrWhiteSpace(ItemName))
            {
                UiHelpers.ShowWarning(this, "Попълнете кода и наименованието.");
                return;
            }

            DialogResult = DialogResult.OK;
            Close();
        };

        var cancel = UiHelpers.CreateButton("Отказ", 110);
        cancel.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };

        Controls.Add(layout);
        Controls.Add(UiHelpers.DialogButtons(ok, cancel));
        Controls.Add(UiHelpers.CreateHeader(title, "Номенклатурна позиция"));

        AcceptButton = ok;
        CancelButton = cancel;
    }
}
