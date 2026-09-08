using PharmacyIS.Domain.Entities;
using PharmacyIS.Domain.Enums;
using PharmacyIS.Services.Models;
using PharmacyIS.UI.Infrastructure;

namespace PharmacyIS.UI.Forms;

/// <summary>Администриране на потребителските профили. Достъпно само за управител.</summary>
public class UsersForm : BaseChildForm
{
    private readonly DataGridView _grid = new();
    private readonly Label _summary = UiHelpers.SummaryLabel();

    public UsersForm()
        : base("Потребители", "Профили на служителите и техните права за достъп")
    {
        AddToolbarButton("Нов потребител", AddUser, 150, primary: true);
        AddToolbarButton("Редактирай", EditUser, 140);
        AddToolbarSeparator();
        AddToolbarButton("Обнови", LoadData, 110);

        UiHelpers.StyleGrid(_grid);
        _grid.Dock = DockStyle.Fill;
        _grid.Columns.AddRange(
            UiHelpers.TextColumn(nameof(User.Username), "Потребителско име", 140),
            UiHelpers.TextColumn(nameof(User.FullName), "Име и фамилия", 220),
            UiHelpers.TextColumn(nameof(User.RoleName), "Роля", 110),
            UiHelpers.TextColumn(nameof(User.IsActive), "Активен", 80),
            UiHelpers.TextColumn(nameof(User.CreatedAt), "Създаден на", 120, "dd.MM.yyyy"));

        _grid.CellDoubleClick += (_, e) => { if (e.RowIndex >= 0) Run(EditUser); };
        _grid.CellFormatting += (_, e) =>
        {
            if (_grid.Rows[e.RowIndex].DataBoundItem is not User user) return;

            if (_grid.Columns[e.ColumnIndex].DataPropertyName == nameof(User.IsActive))
                e.Value = user.IsActive ? "да" : "не";

            if (!user.IsActive)
            {
                _grid.Rows[e.RowIndex].DefaultCellStyle.ForeColor = UiHelpers.Muted;
                _grid.Rows[e.RowIndex].DefaultCellStyle.BackColor = Color.FromArgb(246, 247, 247);
            }
        };

        Content.Controls.Add(_grid);
        Content.Controls.Add(_summary);
        Load += (_, _) => Run(LoadData);
    }

    public override void OnPageActivated() => Run(LoadData);

    private void LoadData()
    {
        var users = AppSession.Auth.GetAllUsers(AppSession.CurrentUser!);
        UiHelpers.Bind(_grid, users);
        _summary.Text = $"Профили: {users.Count} · активни: {users.Count(u => u.IsActive)} · " +
                        $"управители: {users.Count(u => u.Role == UserRole.Manager && u.IsActive)}";
    }

    private User? Selected => _grid.CurrentRow?.DataBoundItem as User;

    private void AddUser()
    {
        using var dialog = new UserEditForm(null);
        if (dialog.ShowDialog(this) == DialogResult.OK) LoadData();
    }

    private void EditUser()
    {
        if (Selected is null)
        {
            UiHelpers.ShowWarning(this, "Изберете потребител от списъка.");
            return;
        }

        using var dialog = new UserEditForm(Selected);
        if (dialog.ShowDialog(this) == DialogResult.OK) LoadData();
    }
}

/// <summary>
/// Диалог за създаване и редактиране на потребителски профил.
/// Паролата се въвежда два пъти за потвърждение. При редактиране полетата
/// за парола могат да останат празни – тогава паролата не се променя.
/// </summary>
public class UserEditForm : Form
{
    private readonly User _user;
    private readonly bool _isNew;

    private readonly TextBox _username = new();
    private readonly TextBox _fullName = new();
    private readonly ComboBox _role = new();
    private readonly TextBox _password = new();
    private readonly TextBox _confirm = new();
    private readonly CheckBox _active = new();
    private readonly Label _passwordHint = new();

    public UserEditForm(User? user)
    {
        _isNew = user is null;
        _user = user is null
            ? new User { IsActive = true, Role = UserRole.Pharmacist }
            : new User
            {
                UserId = user.UserId,
                Username = user.Username,
                FullName = user.FullName,
                Role = user.Role,
                IsActive = user.IsActive
            };

        UiHelpers.StyleDialog(this,
            _isNew ? "Нов потребител" : $"Редактиране: {_user.Username}", 600, 560);

        BuildLayout();
    }

    private void BuildLayout()
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            AutoScroll = true,
            Padding = new Padding(24, 18, 24, 10),
            BackColor = Color.White
        };

        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 200));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        foreach (var box in new[] { _username, _fullName, _password, _confirm })
        {
            box.Width = 300;
            box.Font = UiHelpers.FontInput;
        }

        _username.Text = _user.Username;
        _fullName.Text = _user.FullName;
        _password.UseSystemPasswordChar = true;
        _confirm.UseSystemPasswordChar = true;

        _role.Width = 300;
        _role.Font = UiHelpers.FontInput;
        _role.DropDownStyle = ComboBoxStyle.DropDownList;
        _role.Items.AddRange(new object[] { "Фармацевт", "Управител" });
        _role.SelectedIndex = _user.Role == UserRole.Manager ? 1 : 0;

        _active.Text = "Активен профил";
        _active.AutoSize = true;
        _active.Checked = _user.IsActive;
        _active.Font = UiHelpers.FontBody;

        _passwordHint.Text = _isNew
            ? "Паролата трябва да е дълга поне 8 знака и да съдържа\nпоне една буква и поне една цифра."
            : "Оставете двете полета празни, ако паролата\nне трябва да се променя.";
        _passwordHint.ForeColor = UiHelpers.Muted;
        _passwordHint.Font = UiHelpers.FontSmall;
        _passwordHint.AutoSize = true;

        AddRow(layout, "Потребителско име", _username);
        AddRow(layout, "Име и фамилия", _fullName);
        AddRow(layout, "Роля", _role);
        AddRow(layout, string.Empty, _active);
        AddRow(layout, string.Empty, Divider());
        AddRow(layout, _isNew ? "Парола" : "Нова парола", _password);
        AddRow(layout, "Повторете паролата", _confirm);
        AddRow(layout, string.Empty, _passwordHint);

        var save = UiHelpers.CreateButton("Запис", 120, primary: true);
        save.Click += OnSave;

        var cancel = UiHelpers.CreateButton("Отказ", 120);
        cancel.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };

        Controls.Add(layout);
        Controls.Add(UiHelpers.DialogButtons(save, cancel));
        Controls.Add(UiHelpers.CreateHeader(
            _isNew ? "Нов потребител" : "Редактиране на потребител",
            "Ролята определя достъпа до модулите на системата"));

        AcceptButton = save;
        CancelButton = cancel;
    }

    private static Control Divider() => new Panel
    {
        Height = 1,
        Width = 300,
        BackColor = UiHelpers.Border,
        Margin = new Padding(0, 8, 0, 8)
    };

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

    private void OnSave(object? sender, EventArgs e)
    {
        try
        {
            _user.Username = _username.Text.Trim();
            _user.FullName = _fullName.Text.Trim();
            _user.Role = _role.SelectedIndex == 1 ? UserRole.Manager : UserRole.Pharmacist;
            _user.IsActive = _active.Checked;

            var wantsPassword = _isNew || _password.Text.Length > 0 || _confirm.Text.Length > 0;

            if (wantsPassword && _password.Text != _confirm.Text)
            {
                UiHelpers.ShowWarning(this, "Двете въведени пароли не съвпадат.");
                _confirm.Focus();
                return;
            }

            if (_isNew)
            {
                AppSession.Auth.CreateUser(AppSession.CurrentUser!, _user, _password.Text);
            }
            else
            {
                AppSession.Auth.UpdateUser(AppSession.CurrentUser!, _user);

                if (wantsPassword)
                {
                    AppSession.Auth.ResetPassword(AppSession.CurrentUser!,
                        _user.UserId, _password.Text);
                }
            }

            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex)
        {
            UiHelpers.ShowError(this, ex);
        }
    }
}

/// <summary>Смяна на собствената парола на влезлия потребител.</summary>
public class ChangePasswordForm : Form
{
    private readonly TextBox _current = new();
    private readonly TextBox _password = new();
    private readonly TextBox _confirm = new();

    public ChangePasswordForm()
    {
        UiHelpers.StyleDialog(this, "Смяна на паролата", 480, 420);

        var layout = UiHelpers.DialogBody(new Padding(26, 18, 26, 12));

        foreach (var box in new[] { _current, _password, _confirm })
        {
            box.Width = 360;
            box.UseSystemPasswordChar = true;
            box.Font = UiHelpers.FontInput;
            box.Margin = new Padding(0, 0, 0, 12);
        }

        layout.Controls.Add(Caption("Текуща парола"));
        layout.Controls.Add(_current);
        layout.Controls.Add(Caption("Нова парола"));
        layout.Controls.Add(_password);
        layout.Controls.Add(Caption("Повторете новата парола"));
        layout.Controls.Add(_confirm);
        layout.Controls.Add(new Label
        {
            Text = "Паролата трябва да е дълга поне 8 знака и да съдържа\n"
                   + "поне една буква и поне една цифра.",
            ForeColor = UiHelpers.Muted,
            Font = UiHelpers.FontSmall,
            AutoSize = true,
            Margin = new Padding(0)
        });

        var ok = UiHelpers.CreateButton("Смени паролата", 150, primary: true);
        ok.Click += OnChange;

        var cancel = UiHelpers.CreateButton("Отказ", 120);
        cancel.Click += (_, _) => Close();

        Controls.Add(layout);
        Controls.Add(UiHelpers.DialogButtons(ok, cancel));
        Controls.Add(UiHelpers.CreateHeader("Смяна на паролата",
            $"Профил: {AppSession.CurrentUser?.Username}"));

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

    private void OnChange(object? sender, EventArgs e)
    {
        try
        {
            if (_password.Text != _confirm.Text)
            {
                UiHelpers.ShowWarning(this, "Двете въведени пароли не съвпадат.");
                _confirm.Focus();
                return;
            }

            AppSession.Auth.ChangePassword(AppSession.CurrentUser!.UserId,
                _current.Text, _password.Text);

            UiHelpers.ShowInfo(this, "Паролата е сменена успешно.");
            Close();
        }
        catch (Exception ex)
        {
            UiHelpers.ShowError(this, ex);
        }
    }
}

/// <summary>Преглед на одитния дневник на системата.</summary>
public class AuditLogForm : BaseChildForm
{
    private readonly DataGridView _grid = new();
    private readonly Label _summary = UiHelpers.SummaryLabel();

    public AuditLogForm()
        : base("Одитен дневник", "Записи за входовете в системата и действията с повишен риск")
    {
        AddToolbarButton("Обнови", LoadData, 110, primary: true);
        AddToolbarButton("Изнеси в CSV", () => UiHelpers.ExportGridToCsv(this, _grid, "ОдитенДневник"), 150);

        UiHelpers.StyleGrid(_grid);
        _grid.Dock = DockStyle.Fill;
        _grid.Columns.AddRange(
            UiHelpers.TextColumn(nameof(AuditLogEntry.EventDate), "Дата и час", 130, "dd.MM.yyyy HH:mm:ss"),
            UiHelpers.TextColumn(nameof(AuditLogEntry.Username), "Потребител", 130),
            UiHelpers.TextColumn(nameof(AuditLogEntry.Action), "Събитие", 150),
            UiHelpers.TextColumn(nameof(AuditLogEntry.IsSuccess), "Резултат", 80),
            UiHelpers.TextColumn(nameof(AuditLogEntry.Details), "Подробности", 300));

        _grid.CellFormatting += (_, e) =>
        {
            if (_grid.Rows[e.RowIndex].DataBoundItem is not AuditLogEntry entry) return;

            if (_grid.Columns[e.ColumnIndex].DataPropertyName == nameof(AuditLogEntry.IsSuccess))
                e.Value = entry.IsSuccess ? "успех" : "отказ";

            if (!entry.IsSuccess)
                _grid.Rows[e.RowIndex].DefaultCellStyle.BackColor = UiHelpers.Danger;
        };

        Content.Controls.Add(_grid);
        Content.Controls.Add(_summary);
        Load += (_, _) => Run(LoadData);
    }

    public override void OnPageActivated() => Run(LoadData);

    private void LoadData()
    {
        var entries = AppSession.Auth.GetAuditLog(AppSession.CurrentUser!, 500);
        UiHelpers.Bind(_grid, entries);
        _summary.Text = $"Показани записи: {entries.Count} · " +
                        $"неуспешни действия: {entries.Count(x => !x.IsSuccess)}";
    }
}

/// <summary>
/// Системни настройки на приложението. Данните за обекта се отпечатват
/// върху издаваните документи и се показват в заглавната лента, затова се
/// поддържат от този екран, а не се променят направо в базата.
/// </summary>
public class SettingsForm : BaseChildForm
{
    private readonly Action _onSettingsChanged;

    private readonly TextBox _pharmacyName = new();
    private readonly TextBox _address = new();
    private readonly NumericUpDown _vatRate = new();
    private readonly NumericUpDown _expiryDays = new();
    private readonly DataGridView _grid = new();

    public SettingsForm(Action onSettingsChanged)
        : base("Системни настройки", "Параметри, определящи поведението на системата")
    {
        _onSettingsChanged = onSettingsChanged;

        AddToolbarButton("Запиши промените", Save, 170, primary: true);
        AddToolbarButton("Отмени промените", LoadData, 170);

        BuildLayout();
        Load += (_, _) => Run(LoadData);
    }

    public override void OnPageActivated() => Run(LoadData);

    private void BuildLayout()
    {
        _vatRate.Minimum = 0;
        _vatRate.Maximum = 100;

        _expiryDays.Minimum = 1;
        _expiryDays.Maximum = 365;

        var outlet = UiHelpers.CreateFilterBar();
        outlet.Controls.Add(UiHelpers.LabeledField("Наименование на аптеката", _pharmacyName, 300));
        outlet.Controls.Add(UiHelpers.LabeledField("Адрес на обекта", _address, 380));
        outlet.Controls.Add(UiHelpers.LabeledField("Ставка на ДДС (%)", _vatRate, 110));

        var warning = UiHelpers.CreateFilterBar();
        warning.Controls.Add(UiHelpers.LabeledField(
            "Предупреждение за изтичащ срок (дни)", _expiryDays, 120));
        warning.Controls.Add(UiHelpers.BareField(new Label
        {
            Text = "Продукт с партида, чийто срок изтича в рамките на този брой\n"
                   + "дни, се извежда в предупрежденията.",
            ForeColor = UiHelpers.Muted,
            Font = UiHelpers.FontSmall,
            AutoSize = true,
            Margin = new Padding(0, 4, 0, 0)
        }));

        UiHelpers.StyleGrid(_grid);
        _grid.Dock = DockStyle.Fill;
        _grid.Columns.AddRange(
            UiHelpers.TextColumn(nameof(SettingRow.Key), "Настройка", 150),
            UiHelpers.TextColumn(nameof(SettingRow.Value), "Стойност", 200),
            UiHelpers.TextColumn(nameof(SettingRow.Description), "Описание", 400));

        Content.Controls.Add(_grid);
        Content.Controls.Add(UiHelpers.SectionTitle("Всички записани настройки"));
        Content.Controls.Add(warning);
        Content.Controls.Add(UiHelpers.SectionTitle("Предупреждения"));
        Content.Controls.Add(outlet);
        Content.Controls.Add(UiHelpers.SectionTitle("Данни за търговския обект"));
    }

    private void LoadData()
    {
        var user = AppSession.CurrentUser!;
        var profile = AppSession.Settings.GetProfile();

        _pharmacyName.Text = profile.Name;
        _address.Text = profile.Address;
        _vatRate.Value = Math.Clamp(profile.VatRate, 0, 100);
        _expiryDays.Value = Math.Clamp(AppSession.Stock.GetExpiryWarningDays(), 1, 365);

        UiHelpers.Bind(_grid, AppSession.Settings.GetAll(user));
    }

    private void Save()
    {
        var user = AppSession.CurrentUser!;

        AppSession.Settings.SaveProfile(user, new PharmacyProfile
        {
            Name = _pharmacyName.Text,
            Address = _address.Text,
            VatRate = (int)_vatRate.Value
        });

        AppSession.Stock.SetExpiryWarningDays(user, (int)_expiryDays.Value);
        AppSession.ReloadPharmacyProfile();

        UiHelpers.ShowInfo(this, "Настройките са записани.");
        LoadData();
        _onSettingsChanged();
    }
}

/// <summary>
/// Контрол на съответствието между наличностите по партиди и складовия
/// журнал. Резултатът се извежда в таблица, а не като съобщение, за да
/// може да бъде разгледан и изнесен.
/// </summary>
public class StockIntegrityForm : BaseChildForm
{
    private readonly DataGridView _grid = new();
    private readonly Label _verdict = new();

    public StockIntegrityForm()
        : base("Контрол на наличностите",
               "Сравнение на количествата по партиди със сумата от складовия журнал")
    {
        AddToolbarButton("Обнови", RunCheck, 110, primary: true);
        AddToolbarButton("Изнеси в CSV", () => UiHelpers.ExportGridToCsv(this, _grid, "Контрол"), 150);

        BuildLayout();
        Load += (_, _) => Run(RunCheck);
    }

    public override void OnPageActivated() => Run(RunCheck);

    private void BuildLayout()
    {
        _verdict.Dock = DockStyle.Top;
        _verdict.AutoSize = false;
        _verdict.Height = 62;
        _verdict.TextAlign = ContentAlignment.MiddleLeft;
        _verdict.Font = UiHelpers.FontSection;
        _verdict.Padding = new Padding(16, 0, 16, 0);
        _verdict.Margin = new Padding(0, 0, 0, 12);

        UiHelpers.StyleGrid(_grid);
        _grid.Dock = DockStyle.Fill;
        _grid.Columns.AddRange(
            UiHelpers.TextColumn("MedicineName", "Лекарствен продукт", 260),
            UiHelpers.TextColumn("BatchNumber", "Партида", 120),
            UiHelpers.TextColumn("Stored", "Записано количество", 110, rightAligned: true),
            UiHelpers.TextColumn("Calculated", "Изчислено от журнала", 120, rightAligned: true),
            UiHelpers.TextColumn("Difference", "Разлика", 90, rightAligned: true));

        _grid.CellFormatting += (_, e) =>
            _grid.Rows[e.RowIndex].DefaultCellStyle.BackColor = UiHelpers.Danger;

        Content.Controls.Add(_grid);
        Content.Controls.Add(UiHelpers.SectionTitle("Установени несъответствия"));
        Content.Controls.Add(_verdict);
    }

    private void RunCheck()
    {
        var rows = AppSession.Stock.CheckStockIntegrity()
            .Select(d => new
            {
                d.MedicineName,
                d.BatchNumber,
                d.Stored,
                d.Calculated,
                Difference = d.Stored - d.Calculated
            })
            .ToList();

        UiHelpers.Bind(_grid, rows);

        if (rows.Count == 0)
        {
            _verdict.Text = "  ✔   Проверката приключи успешно. Наличността по всички партиди "
                            + "съответства на сумата от записите в складовия журнал.";
            _verdict.BackColor = UiHelpers.Success;
            _verdict.ForeColor = Color.FromArgb(21, 87, 36);
        }
        else
        {
            _verdict.Text = $"  ✖   Установени са {rows.Count} несъответствия. "
                            + "Партидите са изброени в таблицата по-долу.";
            _verdict.BackColor = UiHelpers.Danger;
            _verdict.ForeColor = Color.FromArgb(114, 28, 36);
        }
    }
}
