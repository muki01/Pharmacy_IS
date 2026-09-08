using System.Globalization;
using System.Text;
using PharmacyIS.Domain.Exceptions;

namespace PharmacyIS.UI.Infrastructure;

/// <summary>
/// Общи средства за изграждане на интерфейса: цветова схема, единен облик
/// на таблиците и полетата, съобщения и изнасяне на справки във файл.
///
/// Всички съставни елементи се изграждат чрез контейнери с автоматично
/// подреждане, а не чрез задаване на координати. Така при промяна на
/// мащаба на екрана или на дължината на надписите елементите не могат
/// да се застъпят.
/// </summary>
public static class UiHelpers
{
    // ------------------------------------------------------------------
    //  Цветова схема
    // ------------------------------------------------------------------

    /// <summary>Основен цвят на приложението.</summary>
    public static readonly Color Primary = Color.FromArgb(0, 105, 92);

    /// <summary>Потъмнен вариант на основния цвят.</summary>
    public static readonly Color PrimaryDark = Color.FromArgb(0, 77, 64);

    /// <summary>Осветен вариант на основния цвят.</summary>
    public static readonly Color PrimaryLight = Color.FromArgb(0, 137, 123);

    /// <summary>Фон на страничната лента за навигация.</summary>
    public static readonly Color NavBack = Color.FromArgb(250, 251, 251);

    /// <summary>Разделителна линия.</summary>
    public static readonly Color Border = Color.FromArgb(222, 227, 228);

    /// <summary>Основен цвят на текста.</summary>
    public static readonly Color Ink = Color.FromArgb(33, 37, 41);

    /// <summary>Приглушен текст.</summary>
    public static readonly Color Muted = Color.FromArgb(108, 117, 125);

    /// <summary>Фон на редовете с предупреждение.</summary>
    public static readonly Color Warning = Color.FromArgb(255, 243, 205);

    /// <summary>Фон на редовете с критично състояние.</summary>
    public static readonly Color Danger = Color.FromArgb(248, 215, 218);

    /// <summary>Фон на редовете с приход.</summary>
    public static readonly Color Success = Color.FromArgb(212, 237, 218);

    /// <summary>Български формат за числа и дати.</summary>
    public static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("bg-BG");

    // ------------------------------------------------------------------
    //  Шрифтове
    // ------------------------------------------------------------------

    public static Font FontBody { get; } = new("Segoe UI", 9.75f);
    public static Font FontBodyBold { get; } = new("Segoe UI", 9.75f, FontStyle.Bold);
    public static Font FontSmall { get; } = new("Segoe UI", 8.5f);
    public static Font FontSmallBold { get; } = new("Segoe UI", 8.5f, FontStyle.Bold);
    public static Font FontTitle { get; } = new("Segoe UI Semibold", 14f);
    public static Font FontSection { get; } = new("Segoe UI Semibold", 10.5f);
    public static Font FontInput { get; } = new("Segoe UI", 10f);

    /// <summary>Иконата на приложението, ако е налична.</summary>
    public static Icon? AppIcon { get; } = LoadAppIcon();

    private static Icon? LoadAppIcon()
    {
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "app.ico");
            return File.Exists(path) ? new Icon(path) : null;
        }
        catch (Exception ex) when (ex is IOException or ArgumentException)
        {
            return null;
        }
    }

    // ------------------------------------------------------------------
    //  Таблици
    // ------------------------------------------------------------------

    /// <summary>Прилага единен облик върху таблица с данни.</summary>
    public static void StyleGrid(DataGridView grid)
    {
        grid.AutoGenerateColumns = false;
        grid.AllowUserToAddRows = false;
        grid.AllowUserToDeleteRows = false;
        grid.AllowUserToResizeRows = false;
        grid.ReadOnly = true;
        grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        grid.MultiSelect = false;
        grid.RowHeadersVisible = false;
        grid.BorderStyle = BorderStyle.None;
        grid.BackgroundColor = Color.White;
        grid.GridColor = Border;
        grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        grid.EnableHeadersVisualStyles = false;
        grid.ColumnHeadersDefaultCellStyle.BackColor = Primary;
        grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
        grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = Primary;
        grid.ColumnHeadersDefaultCellStyle.SelectionForeColor = Color.White;
        grid.ColumnHeadersDefaultCellStyle.Font = FontBodyBold;
        grid.ColumnHeadersDefaultCellStyle.Padding = new Padding(6, 0, 6, 0);
        grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        grid.ColumnHeadersHeight = 34;
        grid.RowTemplate.Height = 27;
        grid.DefaultCellStyle.Font = FontBody;
        grid.DefaultCellStyle.Padding = new Padding(6, 0, 6, 0);
        grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(178, 223, 219);
        grid.DefaultCellStyle.SelectionForeColor = Ink;
        grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(248, 250, 250);
        grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
    }

    /// <summary>
    /// Свързва таблица с данни така, че подреждането по колона да работи.
    /// Вж. <see cref="SortableBindingList{T}"/>.
    /// </summary>
    public static void Bind<T>(DataGridView grid, IEnumerable<T>? items)
        => grid.DataSource = items is null ? null : new SortableBindingList<T>(items);

    /// <summary>Създава текстова колона за таблица.</summary>
    public static DataGridViewTextBoxColumn TextColumn(string property, string header,
        int fillWeight = 100, string? format = null, bool rightAligned = false)
    {
        var column = new DataGridViewTextBoxColumn
        {
            DataPropertyName = property,
            HeaderText = header,
            FillWeight = fillWeight,
            SortMode = DataGridViewColumnSortMode.Automatic
        };

        if (format is not null)
            column.DefaultCellStyle.Format = format;

        if (rightAligned)
        {
            column.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            column.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleRight;
        }

        return column;
    }

    // ------------------------------------------------------------------
    //  Съобщения
    // ------------------------------------------------------------------

    /// <summary>Показва съобщение за грешка, разбираемо за крайния потребител.</summary>
    public static void ShowError(IWin32Window? owner, Exception exception)
    {
        // Нарушените бизнес правила се показват такива, каквито са формулирани.
        // Останалите грешки се съобщават без технически подробности.
        var message = exception is DomainException
            ? exception.Message
            : $"Възникна непредвидена грешка при изпълнение на операцията.\n\nПодробности: {exception.Message}";

        MessageBox.Show(owner, message, "Грешка", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }

    public static void ShowInfo(IWin32Window? owner, string message, string caption = "Съобщение")
        => MessageBox.Show(owner, message, caption, MessageBoxButtons.OK, MessageBoxIcon.Information);

    public static void ShowWarning(IWin32Window? owner, string message, string caption = "Внимание")
        => MessageBox.Show(owner, message, caption, MessageBoxButtons.OK, MessageBoxIcon.Warning);

    public static bool Confirm(IWin32Window? owner, string message, string caption = "Потвърждение")
        => MessageBox.Show(owner, message, caption, MessageBoxButtons.YesNo, MessageBoxIcon.Question)
            == DialogResult.Yes;

    // ------------------------------------------------------------------
    //  Изнасяне на данни
    // ------------------------------------------------------------------

    /// <summary>
    /// Записва съдържанието на таблица в CSV файл с разделител „точка и запетая“
    /// и кодиране UTF-8 със сигнатура, за да се отваря коректно в Excel.
    /// </summary>
    public static void ExportGridToCsv(IWin32Window? owner, DataGridView grid, string suggestedName)
    {
        if (grid.Rows.Count == 0)
        {
            ShowWarning(owner, "Справката не съдържа редове за изнасяне.");
            return;
        }

        using var dialog = new SaveFileDialog
        {
            Filter = "CSV файл (*.csv)|*.csv",
            FileName = $"{suggestedName}_{DateTime.Now:yyyyMMdd_HHmm}.csv"
        };

        if (dialog.ShowDialog(owner) != DialogResult.OK)
            return;

        var builder = new StringBuilder();

        var headers = grid.Columns.Cast<DataGridViewColumn>()
            .Where(c => c.Visible)
            .ToList();

        builder.AppendLine(string.Join(";", headers.Select(c => Escape(c.HeaderText))));

        foreach (DataGridViewRow row in grid.Rows)
        {
            if (row.IsNewRow) continue;

            var values = headers.Select(c => Escape(row.Cells[c.Index].FormattedValue?.ToString() ?? string.Empty));
            builder.AppendLine(string.Join(";", values));
        }

        try
        {
            File.WriteAllText(dialog.FileName, builder.ToString(), new UTF8Encoding(true));
            ShowInfo(owner, $"Справката е записана във файл:\n{dialog.FileName}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ShowError(owner, new DomainException($"Файлът не може да бъде записан: {ex.Message}"));
        }
    }

    private static string Escape(string value)
        => value.Contains(';') || value.Contains('"') || value.Contains('\n')
            ? $"\"{value.Replace("\"", "\"\"")}\""
            : value;

    // ------------------------------------------------------------------
    //  Съставни елементи на интерфейса
    // ------------------------------------------------------------------

    /// <summary>
    /// Заглавна лента на страница: наименование, пояснение и тънка
    /// разделителна линия отдолу.
    /// </summary>
    public static Panel CreatePageHeader(string title, string subtitle)
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 1,
            RowCount = 2,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(16, 12, 16, 10),
            BackColor = Color.White,
            Margin = new Padding(0)
        };

        layout.Controls.Add(new Label
        {
            Text = title,
            Font = FontTitle,
            ForeColor = Ink,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 2)
        });

        layout.Controls.Add(new Label
        {
            Text = subtitle,
            Font = FontSmall,
            ForeColor = Muted,
            AutoSize = true,
            Margin = new Padding(0)
        });

        var host = new Panel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = Color.White,
            Padding = new Padding(0, 0, 0, 1)
        };

        var separator = new Panel { Dock = DockStyle.Bottom, Height = 1, BackColor = Border };

        host.Controls.Add(layout);
        host.Controls.Add(separator);
        return host;
    }

    /// <summary>
    /// Цветна заглавна лента за диалогов прозорец: наименование на бял
    /// текст върху основния цвят и пояснение под него.
    /// </summary>
    public static Panel CreateHeader(string title, string subtitle)
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 1,
            RowCount = 2,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(18, 12, 18, 12),
            BackColor = Primary,
            Margin = new Padding(0)
        };

        layout.Controls.Add(new Label
        {
            Text = title,
            Font = FontTitle,
            ForeColor = Color.White,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 2)
        });

        if (!string.IsNullOrEmpty(subtitle))
        {
            layout.Controls.Add(new Label
            {
                Text = subtitle,
                Font = FontSmall,
                ForeColor = Color.FromArgb(178, 223, 219),
                AutoSize = true,
                Margin = new Padding(0)
            });
        }

        return layout;
    }

    /// <summary>
    /// Поддържа постоянно съотношение между двата панела на разделител,
    /// включително след промяна на размера на прозореца.
    /// </summary>
    public static void KeepSplitRatio(SplitContainer split, double ratio, int minPanel = 220)
    {
        void Apply(object? sender, EventArgs e)
        {
            var total = split.Orientation == Orientation.Vertical ? split.Width : split.Height;

            // Минималният размер се съобразява с наличното място: при малък
            // контейнер изискването за минимум просто отпада.
            var min = Math.Min(minPanel, total / 3);
            var max = total - min - split.SplitterWidth;
            if (max <= min) return;

            try
            {
                split.SplitterDistance = Math.Clamp((int)(total * ratio), min, max);
            }
            catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
            {
                // Контейнерът още няма валиден размер – стойността ще се
                // приложи при следващата промяна на размера.
            }
        }

        split.SizeChanged += Apply;
        Apply(split, EventArgs.Empty);
    }

    /// <summary>Създава бутон с единен облик.</summary>
    public static Button CreateButton(string text, int minWidth = 130, bool primary = false)
    {
        var button = new Button
        {
            Text = text,
            // Ширината се задава като минимална; бутонът се разширява при
            // нужда, за да остане надписът изцяло видим при всеки мащаб.
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            MinimumSize = new Size(minWidth, 32),
            Padding = new Padding(12, 0, 12, 0),
            FlatStyle = FlatStyle.Flat,
            UseVisualStyleBackColor = false,
            BackColor = primary ? Primary : Color.White,
            ForeColor = primary ? Color.White : Ink,
            Font = primary ? FontBodyBold : FontBody,
            Margin = new Padding(0, 0, 8, 0),
            Cursor = Cursors.Hand
        };

        button.FlatAppearance.BorderColor = primary ? Primary : Border;
        button.FlatAppearance.MouseOverBackColor = primary ? PrimaryLight : Color.FromArgb(240, 245, 245);
        return button;
    }

    /// <summary>
    /// Лента с бутони за действие. Използва подреждане в поток, така че
    /// бутоните никога не се застъпват.
    /// </summary>
    public static FlowLayoutPanel CreateToolbar()
        => new()
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            WrapContents = true,
            Padding = new Padding(16, 10, 16, 10),
            BackColor = Color.White,
            Margin = new Padding(0)
        };

    /// <summary>
    /// Лента с филтри. Всеки филтър се добавя чрез <see cref="LabeledField"/>
    /// и се подрежда автоматично.
    /// </summary>
    public static FlowLayoutPanel CreateFilterBar()
        => new()
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            WrapContents = true,
            Padding = new Padding(0, 0, 0, 10),
            BackColor = Color.White,
            Margin = new Padding(0)
        };

    /// <summary>
    /// Поле с надпис над него. Връща контейнер с автоматичен размер,
    /// който се подрежда в лента с филтри или във формуляр.
    /// </summary>
    public static Control LabeledField(string caption, Control control, int width = 0)
    {
        if (width > 0)
        {
            control.Width = width;
            control.MinimumSize = new Size(width, control.MinimumSize.Height);
        }

        control.Margin = new Padding(0);
        control.Font = control is CheckBox ? FontBody : FontInput;

        var layout = new TableLayoutPanel
        {
            ColumnCount = 1,
            RowCount = 2,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Margin = new Padding(0, 0, 14, 0),
            Padding = new Padding(0)
        };

        layout.Controls.Add(new Label
        {
            Text = caption,
            Font = FontSmallBold,
            ForeColor = Muted,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 3)
        });

        layout.Controls.Add(control);
        return layout;
    }

    /// <summary>Поле без надпис, подравнено по долния ръб на съседните полета.</summary>
    public static Control BareField(Control control)
    {
        control.Margin = new Padding(0);

        var layout = new TableLayoutPanel
        {
            ColumnCount = 1,
            RowCount = 2,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Margin = new Padding(0, 0, 14, 0)
        };

        layout.Controls.Add(new Label
        {
            Text = " ",
            Font = FontSmallBold,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 3)
        });

        layout.Controls.Add(control);
        return layout;
    }

    /// <summary>Заглавие на раздел в рамките на страница.</summary>
    public static Label SectionTitle(string text)
        => new()
        {
            Text = text,
            Dock = DockStyle.Top,
            AutoSize = true,
            Font = FontSection,
            ForeColor = Ink,
            Padding = new Padding(0, 0, 0, 6),
            Margin = new Padding(0)
        };

    /// <summary>Обобщаващ ред под таблица.</summary>
    public static Label SummaryLabel(bool emphasised = false)
        => new()
        {
            Dock = DockStyle.Bottom,
            AutoSize = false,
            Height = 30,
            TextAlign = ContentAlignment.MiddleRight,
            Font = emphasised ? FontSection : FontBody,
            ForeColor = emphasised ? Primary : Muted,
            Padding = new Padding(0, 6, 4, 0)
        };

    /// <summary>
    /// Настройва двойка полета за период: задава формата на датата и не
    /// позволява началото да изпревари края. Без това ограничение справката
    /// би върнала празен резултат или грешка при разменени дати.
    /// </summary>
    public static void SetupPeriod(DateTimePicker from, DateTimePicker to,
        int daysBack, Action onChanged)
    {
        foreach (var picker in new[] { from, to })
        {
            picker.Format = DateTimePickerFormat.Custom;
            picker.CustomFormat = "dd.MM.yyyy";
        }

        from.Value = DateTime.Today.AddDays(-daysBack);
        to.Value = DateTime.Today;

        from.ValueChanged += (_, _) =>
        {
            if (from.Value.Date > to.Value.Date) to.Value = from.Value;
            onChanged();
        };

        to.ValueChanged += (_, _) =>
        {
            if (to.Value.Date < from.Value.Date) from.Value = to.Value;
            onChanged();
        };
    }

    /// <summary>Тънка хоризонтална разделителна линия.</summary>
    public static Panel Separator(DockStyle dock = DockStyle.Top)
        => new() { Dock = dock, Height = 1, BackColor = Border, Margin = new Padding(0) };

    /// <summary>Прилага единен облик върху диалогов прозорец.</summary>
    public static void StyleDialog(Form form, string title, int width, int height)
    {
        form.Text = title;
        form.ClientSize = new Size(width, height);
        form.StartPosition = FormStartPosition.CenterParent;
        form.FormBorderStyle = FormBorderStyle.FixedDialog;
        form.MaximizeBox = false;
        form.MinimizeBox = false;
        form.ShowInTaskbar = false;
        form.BackColor = Color.White;
        form.Font = FontBody;
        form.Icon = AppIcon;
    }

    /// <summary>
    /// Съдържание на диалогов прозорец: поток отгоре надолу, в който всяка
    /// контрола заема собствената си височина. Използва се вместо таблица с
    /// една колона – при нея фиксираната височина се разпределя поравно
    /// между редовете и последните полета остават скрити под лентата с
    /// бутоните.
    /// </summary>
    public static FlowLayoutPanel DialogBody(Padding? padding = null)
        => new()
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            Padding = padding ?? new Padding(24, 18, 24, 12),
            BackColor = Color.White
        };

    /// <summary>Лента с бутони в долния край на диалогов прозорец.</summary>
    public static FlowLayoutPanel DialogButtons(params Button[] buttons)
    {
        var panel = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(16, 10, 16, 12),
            BackColor = Color.FromArgb(248, 250, 250)
        };

        foreach (var button in buttons)
        {
            button.Margin = new Padding(8, 0, 0, 0);
            panel.Controls.Add(button);
        }

        return panel;
    }
}
