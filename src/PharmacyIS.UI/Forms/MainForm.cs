using PharmacyIS.UI.Infrastructure;

namespace PharmacyIS.UI.Forms;

/// <summary>
/// Единственият прозорец на приложението.
///
/// Работните страници не се отварят като отделни прозорци, а се вграждат
/// в работната област. Така потребителят вижда една заглавна лента и един
/// набор системни бутони, а навигацията се извършва от страничната лента.
/// </summary>
public class MainForm : Form
{
    private const int NavWidth = 292;

    // --- горна лента ---------------------------------------------------
    private readonly Panel _topBar = new();
    private readonly Label _appTitle = new();
    private readonly Label _pharmacyName = new();
    private readonly Button _userChip = new();
    private readonly ContextMenuStrip _userMenu = new();

    // --- навигация -----------------------------------------------------
    private readonly Panel _navHost = new();
    private readonly FlowLayoutPanel _nav = new();
    private readonly List<NavGroup> _groups = new();
    private readonly List<Button> _navButtons = new();

    // --- работна област ------------------------------------------------
    private readonly Panel _pageHost = new();
    private readonly Panel _emptyState = new();
    private readonly Label _emptyLabel = new();
    private LoginPage? _loginPage;
    private readonly Dictionary<string, BaseChildForm> _pages = new();
    private string? _currentKey;

    // --- лента на състоянието -------------------------------------------
    private readonly StatusStrip _status = new();
    private readonly ToolStripStatusLabel _statusAlerts = new();

    public MainForm()
    {
        Text = "Информационна система за аптека";
        Icon = UiHelpers.AppIcon;
        WindowState = FormWindowState.Maximized;
        MinimumSize = new Size(1180, 720);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.White;
        Font = UiHelpers.FontBody;
        KeyPreview = true;

        BuildLayout();
    }

    // ==================================================================
    //  Изграждане на прозореца
    // ==================================================================

    private void BuildLayout()
    {
        BuildStatusBar();
        BuildPageHost();
        BuildNavigationHost();
        BuildTopBar();

        // Редът на добавяне определя реда на подреждане: първо запълващата
        // област, после прикрепените към ръбовете елементи.
        Controls.Add(_pageHost);
        Controls.Add(_navHost);
        Controls.Add(_topBar);
        Controls.Add(_status);
    }

    private void BuildTopBar()
    {
        _topBar.Dock = DockStyle.Top;
        _topBar.Height = 56;
        _topBar.BackColor = UiHelpers.Primary;
        _topBar.Padding = new Padding(20, 0, 16, 0);

        _appTitle.Text = "Информационна система за аптека";
        _appTitle.Font = new Font("Segoe UI Semibold", 13f);
        _appTitle.ForeColor = Color.White;
        _appTitle.AutoSize = true;

        _pharmacyName.Font = UiHelpers.FontSmall;
        _pharmacyName.ForeColor = Color.FromArgb(178, 223, 219);
        _pharmacyName.AutoSize = true;

        // Заглавният блок и чипът на потребителя се разполагат в таблица с
        // един ред. Котвата вляво/вдясно без вертикална котва оставя
        // контролата центрирана по височината на реда – така двата елемента
        // стоят на една и съща оптична линия независимо от височината им.
        var titleBlock = new TableLayoutPanel
        {
            Anchor = AnchorStyles.Left,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            RowCount = 2,
            Margin = new Padding(0),
            BackColor = Color.Transparent
        };
        titleBlock.Controls.Add(_appTitle);
        titleBlock.Controls.Add(_pharmacyName);

        // Меню на потребителя – смяна на паролата и излизане от профила.
        _userChip.Text = string.Empty;
        _userChip.AutoSize = true;
        _userChip.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        _userChip.MinimumSize = new Size(180, 36);
        _userChip.FlatStyle = FlatStyle.Flat;
        _userChip.FlatAppearance.BorderSize = 1;
        _userChip.FlatAppearance.BorderColor = UiHelpers.PrimaryLight;
        _userChip.FlatAppearance.MouseOverBackColor = UiHelpers.PrimaryLight;
        _userChip.BackColor = UiHelpers.Primary;
        _userChip.ForeColor = Color.White;
        _userChip.Font = UiHelpers.FontBody;
        _userChip.TextAlign = ContentAlignment.MiddleLeft;
        _userChip.Padding = new Padding(12, 0, 12, 0);
        _userChip.Cursor = Cursors.Hand;
        _userChip.Visible = false;
        _userChip.Click += (_, _) => _userMenu.Show(_userChip,
            new Point(0, _userChip.Height));

        _userChip.Anchor = AnchorStyles.Right;
        _userChip.Margin = new Padding(0);

        _userMenu.Font = UiHelpers.FontBody;
        _userMenu.Items.Add(new ToolStripMenuItem("Смяна на паролата", null,
            (_, _) => ChangeOwnPassword()));
        _userMenu.Items.Add(new ToolStripSeparator());
        _userMenu.Items.Add(new ToolStripMenuItem("Изход от профила", null,
            (_, _) => Logout()));

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Margin = new Padding(0),
            BackColor = Color.Transparent
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.Controls.Add(titleBlock, 0, 0);
        layout.Controls.Add(_userChip, 1, 0);

        _topBar.Controls.Add(layout);
    }

    private void BuildNavigationHost()
    {
        _navHost.Dock = DockStyle.Left;
        _navHost.Width = NavWidth;
        _navHost.BackColor = UiHelpers.NavBack;
        _navHost.Padding = new Padding(0, 8, 1, 8);

        _nav.Dock = DockStyle.Fill;
        _nav.FlowDirection = FlowDirection.TopDown;
        _nav.WrapContents = false;
        _nav.AutoScroll = true;
        _nav.BackColor = UiHelpers.NavBack;
        _nav.Padding = new Padding(8, 0, 8, 0);

        _navHost.Controls.Add(_nav);
        _navHost.Controls.Add(new Panel
        {
            Dock = DockStyle.Right,
            Width = 1,
            BackColor = UiHelpers.Border
        });
    }

    private void BuildPageHost()
    {
        _pageHost.Dock = DockStyle.Fill;
        _pageHost.BackColor = Color.White;

        _emptyState.Dock = DockStyle.Fill;
        _emptyState.BackColor = Color.White;
        _emptyLabel.Dock = DockStyle.Fill;
        _emptyLabel.TextAlign = ContentAlignment.MiddleCenter;
        _emptyLabel.Font = UiHelpers.FontInput;
        _emptyLabel.ForeColor = UiHelpers.Muted;
        _emptyState.Controls.Add(_emptyLabel);

        _pageHost.Controls.Add(_emptyState);
    }

    private void BuildStatusBar()
    {
        _status.BackColor = Color.FromArgb(248, 250, 250);
        _status.Font = UiHelpers.FontBody;
        _status.SizingGrip = false;
        _status.Padding = new Padding(14, 0, 14, 0);
        _status.AutoSize = false;
        _status.Height = 30;

        // Влезлият потребител се вижда в горния десен ъгъл, а използваната
        // база – в прозореца „За програмата“. Лентата остава само за
        // предупрежденията, затова текстът в нея е с по-едър шрифт.
        _statusAlerts.Spring = true;
        _statusAlerts.TextAlign = ContentAlignment.MiddleLeft;
        _statusAlerts.Font = UiHelpers.FontBodyBold;

        _status.Items.Add(_statusAlerts);
    }

    // ==================================================================
    //  Вход и изход от профил
    // ==================================================================

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        _pharmacyName.Text = AppSession.PharmacyName;

        if (AppSession.CurrentUser is null)
            ShowLogin();
    }

    /// <summary>
    /// Извежда страницата за вход. Тя заема цялата работна област, а
    /// страничната лента и менюто на потребителя остават скрити.
    /// </summary>
    private void ShowLogin()
    {
        _navHost.Visible = false;
        _userChip.Visible = false;
        _emptyState.Visible = false;

        if (_loginPage is null)
        {
            _loginPage = new LoginPage(OnLoginSucceeded, CloseApplication);
            _pageHost.Controls.Add(_loginPage);
        }

        _loginPage.Visible = true;
        _loginPage.Show();
        _loginPage.BringToFront();
        _loginPage.Reset();
    }

    /// <summary>Подготвя работната среда след успешно удостоверяване.</summary>
    private void OnLoginSucceeded()
    {
        if (_loginPage is not null)
            _loginPage.Visible = false;

        _navHost.Visible = true;
        _emptyLabel.Text = "Изберете раздел от менюто вляво.";

        BuildNavigation();
        UpdateUserChip();
        RefreshAlerts();
        Navigate("home");
    }

    /// <summary>Затваря приложението от страницата за вход.</summary>
    private void CloseApplication()
    {
        if (!UiHelpers.Confirm(this, "Желаете ли да затворите приложението?", "Затваряне"))
            return;

        _closingConfirmed = true;
        Close();
    }

    /// <summary>Прекратява текущата сесия и връща екрана за вход.</summary>
    private void Logout()
    {
        if (!UiHelpers.Confirm(this,
                "Желаете ли да излезете от профила си?\n\n" +
                "Приложението остава отворено и ще поиска ново удостоверяване.",
                "Изход от профила"))
        {
            return;
        }

        ClearPages();
        _nav.Controls.Clear();
        _groups.Clear();
        _navButtons.Clear();
        _userChip.Visible = false;
        _emptyLabel.Text = string.Empty;
        _statusAlerts.Text = string.Empty;

        AppSession.SignOut();
        ShowLogin();
    }

    private void UpdateUserChip()
    {
        var user = AppSession.CurrentUser;
        if (user is null) return;

        _userChip.Text = $"{user.FullName}  ·  {user.RoleName}   ▾";
        _userChip.Visible = true;
    }

    private void ChangeOwnPassword()
    {
        using var dialog = new ChangePasswordForm();
        dialog.ShowDialog(this);
    }

    // ==================================================================
    //  Навигация
    // ==================================================================

    private void BuildNavigation()
    {
        _nav.Controls.Clear();
        _groups.Clear();
        _navButtons.Clear();

        var isManager = AppSession.IsManager;

        AddSingleItem("Начало", "home", () => new DashboardForm(RefreshAlerts, Navigate));

        AddGroup("Продажби", new[]
        {
            new NavEntry("Нова продажба", "sale", () => new SaleForm(RefreshAlerts), "Ctrl+N"),
            new NavEntry("Регистър на продажбите", "sales-journal", () => new SalesJournalForm(), "Ctrl+R"),
        });

        // Заприхождаването е ежедневна операция и е достъпно за двете роли.
        // Регистърът на доставките показва стойности по документи и остава
        // за управителя.
        var warehouse = new List<NavEntry>
        {
            new("Наличности и партиди", "stock", () => new StockForm(RefreshAlerts), "Ctrl+S"),
            new("Нова доставка", "delivery", () => new DeliveryForm(RefreshAlerts), "Ctrl+D"),
            new("Складови движения", "movements", () => new MovementsForm()),
        };

        if (isManager)
        {
            warehouse.Insert(2, new NavEntry("Регистър на доставките", "deliveries-journal",
                () => new DeliveriesJournalForm()));
        }

        AddGroup("Склад", warehouse.ToArray());

        // Каталогът е нужен и на гишето – фармацевтът го отваря за справка,
        // но не поддържа асортимента, цените и данните на контрагентите.
        var catalog = new List<NavEntry>
        {
            new("Лекарствени продукти", "medicines", () => new MedicinesForm(), "Ctrl+M"),
        };

        if (isManager)
        {
            catalog.Add(new NavEntry("Доставчици", "suppliers", () => new SuppliersForm()));
            catalog.Add(new NavEntry("Съставки и форми", "nomenclature", () => new NomenclatureForm()));
        }

        AddGroup("Номенклатури", catalog.ToArray());

        var reports = new List<NavEntry>
        {
            new("Складова наличност", "rep-stock",
                () => new ReportsForm(ReportsForm.ReportKind.Stock)),
            new("Изтичащи срокове", "rep-expiry",
                () => new ReportsForm(ReportsForm.ReportKind.Expiry)),
        };

        if (isManager)
        {
            reports.AddRange(new[]
            {
                new NavEntry("Дневен оборот", "rep-turnover",
                    () => new ReportsForm(ReportsForm.ReportKind.DailyTurnover)),
                new NavEntry("Най-продавани продукти", "rep-top",
                    () => new ReportsForm(ReportsForm.ReportKind.TopProducts)),
                new NavEntry("Продажби по служители", "rep-users",
                    () => new ReportsForm(ReportsForm.ReportKind.SalesByUser)),
                new NavEntry("Доставки по доставчици", "rep-suppliers",
                    () => new ReportsForm(ReportsForm.ReportKind.DeliveriesBySupplier)),
                new NavEntry("Бракувани количества", "rep-writeoffs",
                    () => new ReportsForm(ReportsForm.ReportKind.WriteOffs)),
            });
        }

        AddGroup("Справки", reports.ToArray());

        if (isManager)
        {
            AddGroup("Администрация", new[]
            {
                new NavEntry("Потребители", "users", () => new UsersForm()),
                new NavEntry("Системни настройки", "settings", () => new SettingsForm(OnSettingsChanged)),
                new NavEntry("Одитен дневник", "audit", () => new AuditLogForm()),
                new NavEntry("Контрол на наличностите", "integrity", () => new StockIntegrityForm()),
            });
        }

        AddGroup("Помощ", new[]
        {
            new NavEntry("Ръководство", "help", () => new HelpForm(), "F1"),
        });

        AddAboutButton();
    }

    private void AddSingleItem(string title, string key, Func<BaseChildForm> factory)
    {
        var button = CreateNavButton(title, bold: true, indent: false);
        button.Tag = key;
        button.Click += (_, _) => Navigate(key, factory);
        _navButtons.Add(button);
        _nav.Controls.Add(button);
        _pageFactories[key] = factory;

        _nav.Controls.Add(new Panel
        {
            Width = NavWidth - 36,
            Height = 10,
            BackColor = Color.Transparent,
            Margin = new Padding(0)
        });
    }

    private readonly Dictionary<string, Func<BaseChildForm>> _pageFactories = new();

    private void AddGroup(string title, NavEntry[] entries)
    {
        if (entries.Length == 0) return;

        var header = new Button
        {
            Text = "▸   " + title.ToUpperInvariant(),
            Width = NavWidth - 34,
            Height = 32,
            TextAlign = ContentAlignment.MiddleLeft,
            FlatStyle = FlatStyle.Flat,
            BackColor = UiHelpers.NavBack,
            ForeColor = UiHelpers.Muted,
            Font = UiHelpers.FontSmallBold,
            Padding = new Padding(6, 0, 0, 0),
            Margin = new Padding(0, 2, 0, 2),
            Cursor = Cursors.Hand,
            UseVisualStyleBackColor = false
        };
        header.FlatAppearance.BorderSize = 0;
        header.FlatAppearance.MouseOverBackColor = Color.FromArgb(238, 242, 242);

        var items = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Width = NavWidth - 34,
            Margin = new Padding(0, 0, 0, 6),
            Visible = false,
            BackColor = UiHelpers.NavBack
        };

        foreach (var entry in entries)
        {
            var button = CreateNavButton(entry.Title, bold: false, indent: true, entry.Shortcut);
            button.Tag = entry.Key;
            var key = entry.Key;
            var factory = entry.Factory;
            button.Click += (_, _) => Navigate(key, factory);
            _navButtons.Add(button);
            items.Controls.Add(button);
            _pageFactories[key] = factory;
        }

        var group = new NavGroup(title, header, items);
        header.Click += (_, _) => ToggleGroup(group);
        _groups.Add(group);

        _nav.Controls.Add(header);
        _nav.Controls.Add(items);
    }

    private void AddAboutButton()
    {
        var button = CreateNavButton("За програмата", bold: false, indent: false);
        button.ForeColor = UiHelpers.Muted;
        button.Click += (_, _) =>
        {
            using var about = new AboutForm();
            about.ShowDialog(this);
        };

        _nav.Controls.Add(new Panel
        {
            Width = NavWidth - 36,
            Height = 8,
            BackColor = Color.Transparent
        });
        _nav.Controls.Add(button);
    }

    private static readonly ToolTip NavTooltip = new() { AutoPopDelay = 8000, InitialDelay = 500 };

    private static Button CreateNavButton(string text, bool bold, bool indent, string? shortcut = null)
    {
        var button = new Button
        {
            Text = text,
            Width = NavWidth - (indent ? 44 : 34),
            Height = 34,
            TextAlign = ContentAlignment.MiddleLeft,
            FlatStyle = FlatStyle.Flat,
            BackColor = UiHelpers.NavBack,
            ForeColor = UiHelpers.Ink,
            Font = bold ? UiHelpers.FontBodyBold : UiHelpers.FontBody,
            Padding = new Padding(indent ? 14 : 10, 0, 4, 0),
            Margin = new Padding(indent ? 10 : 0, 1, 0, 1),
            Cursor = Cursors.Hand,
            UseVisualStyleBackColor = false,
            // Ако надписът не се побира, се съкращава с многоточие вместо
            // да се отрязва по средата на дума.
            AutoEllipsis = true
        };

        button.FlatAppearance.BorderSize = 0;
        button.FlatAppearance.MouseOverBackColor = Color.FromArgb(232, 240, 239);

        NavTooltip.SetToolTip(button, shortcut is null ? text : $"{text}   ({shortcut})");
        return button;
    }

    private void ToggleGroup(NavGroup group)
    {
        var opening = !group.Items.Visible;

        // Разгъната остава само една група – така списъкът остава кратък.
        foreach (var other in _groups)
            SetGroupExpanded(other, other == group && opening);
    }

    private static void SetGroupExpanded(NavGroup group, bool expanded)
    {
        group.Items.Visible = expanded;
        group.Header.Text = (expanded ? "▾   " : "▸   ") + group.Title.ToUpperInvariant();
        group.Header.ForeColor = expanded ? UiHelpers.Primary : UiHelpers.Muted;
    }

    /// <summary>Отваря страница по вече регистриран ключ.</summary>
    public void Navigate(string key)
    {
        if (_pageFactories.TryGetValue(key, out var factory))
            Navigate(key, factory);
    }

    /// <summary>Извежда страница в работната област, като я създава при нужда.</summary>
    private void Navigate(string key, Func<BaseChildForm> factory)
    {
        try
        {
            Cursor = Cursors.WaitCursor;

            if (!_pages.TryGetValue(key, out var page))
            {
                page = factory();
                page.TopLevel = false;
                page.Dock = DockStyle.Fill;
                _pageHost.Controls.Add(page);
                _pages[key] = page;
            }

            _emptyState.Visible = false;

            foreach (var other in _pages.Values)
                other.Visible = other == page;

            page.Show();
            page.BringToFront();
            page.OnPageActivated();

            _currentKey = key;
            HighlightNavigation(key);
            ExpandGroupOf(key);
        }
        catch (Exception ex)
        {
            UiHelpers.ShowError(this, ex);
        }
        finally
        {
            Cursor = Cursors.Default;
        }
    }

    private void HighlightNavigation(string key)
    {
        foreach (var button in _navButtons)
        {
            var selected = Equals(button.Tag, key);
            button.BackColor = selected ? Color.FromArgb(224, 242, 241) : UiHelpers.NavBack;
            button.ForeColor = selected ? UiHelpers.PrimaryDark : UiHelpers.Ink;
            button.Font = selected ? UiHelpers.FontBodyBold : button.Font;
            if (!selected && button.Font.Bold && !Equals(button.Tag, "home"))
                button.Font = UiHelpers.FontBody;
        }
    }

    private void ExpandGroupOf(string key)
    {
        foreach (var group in _groups)
        {
            var contains = group.Items.Controls.OfType<Button>()
                .Any(b => Equals(b.Tag, key));

            if (contains)
                SetGroupExpanded(group, true);
        }
    }

    private void ClearPages()
    {
        foreach (var page in _pages.Values)
        {
            _pageHost.Controls.Remove(page);
            page.Dispose();
        }

        _pages.Clear();
        _pageFactories.Clear();
        _currentKey = null;
        _emptyState.Visible = true;
    }

    // ==================================================================
    //  Предупреждения и бързи клавиши
    // ==================================================================

    /// <summary>Обновява обобщението на предупрежденията в лентата на състоянието.</summary>
    /// <summary>
    /// Прилага променените системни настройки: обновява наименованието в
    /// заглавната лента и преизчислява предупрежденията, чийто праг може
    /// да е бил променен.
    /// </summary>
    private void OnSettingsChanged()
    {
        _pharmacyName.Text = AppSession.Pharmacy.Name;
        RefreshAlerts();
    }

    private void RefreshAlerts()
    {
        if (AppSession.CurrentUser is null) return;

        try
        {
            var alerts = AppSession.Stock.GetAlerts(AppSession.CurrentUser!);
            _statusAlerts.Text = alerts.Summary;
            _statusAlerts.ForeColor = alerts.HasAlerts ? Color.Firebrick : Color.SeaGreen;
        }
        catch (Exception ex)
        {
            _statusAlerts.Text = "Предупрежденията не могат да бъдат изчислени.";
            _statusAlerts.ForeColor = UiHelpers.Muted;
            System.Diagnostics.Debug.WriteLine(ex);
        }
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (AppSession.CurrentUser is not null)
        {
            switch (keyData)
            {
                case Keys.Control | Keys.N: Navigate("sale"); return true;
                case Keys.Control | Keys.R: Navigate("sales-journal"); return true;
                case Keys.Control | Keys.S: Navigate("stock"); return true;
                case Keys.Control | Keys.D: Navigate("delivery"); return true;
                case Keys.Control | Keys.M: Navigate("medicines"); return true;
                case Keys.Control | Keys.H: Navigate("home"); return true;
                case Keys.F1: Navigate("help"); return true;
            }
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }

    // ==================================================================
    //  Затваряне
    // ==================================================================

    private bool _closingConfirmed;

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (!_closingConfirmed && e.CloseReason == CloseReason.UserClosing &&
            AppSession.CurrentUser is not null &&
            !UiHelpers.Confirm(this, "Желаете ли да затворите приложението?", "Затваряне"))
        {
            e.Cancel = true;
            return;
        }

        base.OnFormClosing(e);
    }

    // ==================================================================
    //  Помощни типове
    // ==================================================================

    /// <summary>Елемент от страничното меню.</summary>
    private sealed record NavEntry(string Title, string Key, Func<BaseChildForm> Factory,
        string? Shortcut = null);

    /// <summary>Група от елементи в страничното меню.</summary>
    private sealed record NavGroup(string Title, Button Header, FlowLayoutPanel Items);
}
