using System.Reflection;
using PharmacyIS.UI.Infrastructure;

namespace PharmacyIS.UI.Forms;

/// <summary>
/// Вградено ръководство за работа. Съдържанието е разделено на теми,
/// които се избират от списък вляво и се извеждат форматирани вдясно.
/// </summary>
public class HelpForm : BaseChildForm
{
    private readonly ListBox _topics = new();
    private readonly Panel _viewer = new();
    private readonly FlowLayoutPanel _article = new();
    private readonly List<Topic> _content = BuildContent(AppSession.IsManager);

    public HelpForm()
        : base("Ръководство за работа",
            AppSession.IsManager
                ? "Описание на операциите, достъпни за роля „Управител“"
                : "Описание на операциите, достъпни за роля „Фармацевт“")
    {
        AddToolbarButton("Предишна тема", () => Step(-1), 150);
        AddToolbarButton("Следваща тема", () => Step(1), 150);

        BuildLayout();
        Load += (_, _) => Run(() => ShowTopic(0));
    }

    // ------------------------------------------------------------------
    //  Изграждане
    // ------------------------------------------------------------------

    private void BuildLayout()
    {
        _topics.Dock = DockStyle.Fill;
        _topics.BorderStyle = BorderStyle.None;
        _topics.Font = UiHelpers.FontBody;
        _topics.ItemHeight = 32;
        _topics.DrawMode = DrawMode.OwnerDrawFixed;
        _topics.BackColor = Color.White;
        _topics.DrawItem += OnDrawTopic;
        _topics.SelectedIndexChanged += (_, _) => Run(() => ShowTopic(_topics.SelectedIndex));

        foreach (var topic in _content)
            _topics.Items.Add(topic.Title);

        var left = new Panel { Dock = DockStyle.Left, Width = 288, BackColor = Color.White };
        left.Controls.Add(_topics);
        left.Controls.Add(UiHelpers.SectionTitle("Теми"));
        left.Controls.Add(new Panel { Dock = DockStyle.Right, Width = 1, BackColor = UiHelpers.Border });

        _article.Dock = DockStyle.Fill;
        _article.FlowDirection = FlowDirection.TopDown;
        _article.WrapContents = false;
        _article.AutoScroll = true;
        _article.BackColor = Color.White;
        _article.Padding = new Padding(24, 4, 24, 24);

        _viewer.Dock = DockStyle.Fill;
        _viewer.BackColor = Color.White;
        _viewer.Controls.Add(_article);

        Content.Padding = new Padding(0);
        Content.Controls.Add(_viewer);
        Content.Controls.Add(left);

        // Ширината на статията се съобразява с размера на прозореца, за да
        // може текстът да се пренася, вместо да излиза извън видимата област.
        _viewer.SizeChanged += (_, _) => ReflowArticle();
    }

    private void OnDrawTopic(object? sender, DrawItemEventArgs e)
    {
        if (e.Index < 0) return;

        var selected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
        var back = selected ? Color.FromArgb(224, 242, 241) : Color.White;
        var fore = selected ? UiHelpers.PrimaryDark : UiHelpers.Ink;

        using (var brush = new SolidBrush(back))
            e.Graphics.FillRectangle(brush, e.Bounds);

        if (selected)
        {
            using var accent = new SolidBrush(UiHelpers.Primary);
            e.Graphics.FillRectangle(accent, e.Bounds.X, e.Bounds.Y, 3, e.Bounds.Height);
        }

        TextRenderer.DrawText(e.Graphics, _topics.Items[e.Index].ToString(),
            selected ? UiHelpers.FontBodyBold : UiHelpers.FontBody,
            new Rectangle(e.Bounds.X + 14, e.Bounds.Y, e.Bounds.Width - 18, e.Bounds.Height),
            fore, TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
    }

    // ------------------------------------------------------------------
    //  Извеждане на тема
    // ------------------------------------------------------------------

    private void Step(int delta)
    {
        var next = Math.Clamp(_topics.SelectedIndex + delta, 0, _content.Count - 1);
        _topics.SelectedIndex = next;
    }

    private void ShowTopic(int index)
    {
        if (index < 0 || index >= _content.Count) return;

        if (_topics.SelectedIndex != index)
        {
            _topics.SelectedIndex = index;
            return;
        }

        _article.SuspendLayout();
        _article.Controls.Clear();

        var topic = _content[index];
        _article.Controls.Add(Heading(topic.Title));

        foreach (var block in topic.Blocks)
            _article.Controls.Add(RenderBlock(block));

        _article.ResumeLayout();
        _article.VerticalScroll.Value = 0;
        ReflowArticle();
    }

    private Control RenderBlock(Block block) => block.Kind switch
    {
        BlockKind.Subheading => Subheading(block.Text),
        BlockKind.Step => Step(block.Text, block.Order),
        BlockKind.Bullet => Bullet(block.Text),
        BlockKind.Note => Note(block.Text),
        BlockKind.Keys => KeyRow(block.Text, block.Extra),
        _ => Paragraph(block.Text)
    };

    private void ReflowArticle()
    {
        var width = Math.Max(320, _viewer.ClientSize.Width - 60);

        foreach (Control control in _article.Controls)
        {
            control.MaximumSize = new Size(width, 0);
            control.Width = width;
        }
    }

    // --- градивни елементи на статията ---------------------------------

    private static Label Heading(string text) => new()
    {
        Text = text,
        Font = new Font("Segoe UI Semibold", 15f),
        ForeColor = UiHelpers.PrimaryDark,
        AutoSize = true,
        Margin = new Padding(0, 12, 0, 12)
    };

    private static Label Subheading(string text) => new()
    {
        Text = text,
        Font = UiHelpers.FontSection,
        ForeColor = UiHelpers.Ink,
        AutoSize = true,
        Margin = new Padding(0, 14, 0, 6)
    };

    private static Label Paragraph(string text) => new()
    {
        Text = text,
        Font = UiHelpers.FontBody,
        ForeColor = UiHelpers.Ink,
        AutoSize = true,
        Margin = new Padding(0, 0, 0, 10)
    };

    private static Control Step(string text, int order)
    {
        var row = new TableLayoutPanel
        {
            ColumnCount = 2,
            RowCount = 1,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Margin = new Padding(0, 0, 0, 8)
        };

        row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 32));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        var badge = new Label
        {
            Text = order.ToString(),
            Font = UiHelpers.FontSmallBold,
            ForeColor = Color.White,
            BackColor = UiHelpers.Primary,
            Size = new Size(22, 22),
            TextAlign = ContentAlignment.MiddleCenter,
            Margin = new Padding(0, 1, 10, 0)
        };

        row.Controls.Add(badge);
        row.Controls.Add(new Label
        {
            Text = text,
            Font = UiHelpers.FontBody,
            ForeColor = UiHelpers.Ink,
            AutoSize = true,
            Margin = new Padding(0)
        });

        return row;
    }

    private static Control Bullet(string text)
    {
        var row = new TableLayoutPanel
        {
            ColumnCount = 2,
            RowCount = 1,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Margin = new Padding(0, 0, 0, 6)
        };

        row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 20));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        row.Controls.Add(new Label
        {
            Text = "•",
            Font = UiHelpers.FontBodyBold,
            ForeColor = UiHelpers.Primary,
            AutoSize = true,
            Margin = new Padding(2, 0, 6, 0)
        });

        row.Controls.Add(new Label
        {
            Text = text,
            Font = UiHelpers.FontBody,
            ForeColor = UiHelpers.Ink,
            AutoSize = true,
            Margin = new Padding(0)
        });

        return row;
    }

    private static Control Note(string text)
    {
        var panel = new Panel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = Color.FromArgb(240, 248, 247),
            Padding = new Padding(14, 10, 14, 10),
            Margin = new Padding(0, 6, 0, 12)
        };

        panel.Controls.Add(new Label
        {
            Text = text,
            Font = UiHelpers.FontBody,
            ForeColor = UiHelpers.PrimaryDark,
            AutoSize = true,
            Margin = new Padding(0)
        });

        panel.Paint += (s, e) =>
        {
            using var brush = new SolidBrush(UiHelpers.Primary);
            e.Graphics.FillRectangle(brush, 0, 0, 3, ((Control)s!).Height);
        };

        return panel;
    }

    private static Control KeyRow(string keys, string description)
    {
        var row = new TableLayoutPanel
        {
            ColumnCount = 2,
            RowCount = 1,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Margin = new Padding(0, 0, 0, 6)
        };

        row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        var key = new Label
        {
            Text = keys,
            Font = new Font("Consolas", 9.5f, FontStyle.Bold),
            ForeColor = UiHelpers.PrimaryDark,
            BackColor = Color.FromArgb(240, 245, 245),
            AutoSize = false,
            Size = new Size(92, 24),
            TextAlign = ContentAlignment.MiddleCenter,
            Margin = new Padding(0, 0, 14, 0)
        };

        key.Paint += (s, e) =>
        {
            using var pen = new Pen(UiHelpers.Border);
            var c = (Control)s!;
            e.Graphics.DrawRectangle(pen, 0, 0, c.Width - 1, c.Height - 1);
        };

        row.Controls.Add(key);
        row.Controls.Add(new Label
        {
            Text = description,
            Font = UiHelpers.FontBody,
            ForeColor = UiHelpers.Ink,
            AutoSize = true,
            Margin = new Padding(0, 3, 0, 0)
        });

        return row;
    }

    // ------------------------------------------------------------------
    //  Съдържание
    // ------------------------------------------------------------------

    private enum BlockKind { Paragraph, Subheading, Step, Bullet, Note, Keys }

    private sealed record Block(BlockKind Kind, string Text, int Order = 0, string Extra = "");

    private sealed record Topic(string Title, List<Block> Blocks);

    private static Block P(string t) => new(BlockKind.Paragraph, t);
    private static Block H(string t) => new(BlockKind.Subheading, t);
    private static Block S(int n, string t) => new(BlockKind.Step, t, n);
    private static Block B(string t) => new(BlockKind.Bullet, t);
    private static Block N(string t) => new(BlockKind.Note, t);
    private static Block K(string keys, string desc) => new(BlockKind.Keys, keys, 0, desc);

    /// <summary>
    /// Изгражда темите за ролята на влезлия потребител. Ръководството не
    /// описва операции, до които ролята няма достъп – така служителят на
    /// гишето чете само това, което може да изпълни.
    /// </summary>
    private static List<Topic> BuildContent(bool isManager)
    {
        var topics = new List<Topic>
        {
            new("Вход в системата и роли", Login(isManager)),
            new("Продажба на лекарства", Selling(isManager)),
            new("Заприхождаване на доставка", Delivery(isManager)),
            new("Склад и предупреждения", Warehouse(isManager)),
            new("Каталог на продуктите", Catalog(isManager)),
            new("Справки", Reports(isManager)),
        };

        if (isManager)
            topics.Add(new Topic("Администриране", Administration()));

        topics.Add(new Topic("Бързи клавиши", Shortcuts(isManager)));

        // Номерацията се поставя накрая, за да остане последователна
        // независимо от това кои теми са включени.
        return topics
            .Select((topic, index) => new Topic($"{index + 1}. {topic.Title}", topic.Blocks))
            .ToList();
    }

    private static List<Block> Login(bool isManager) => isManager
        ? new List<Block>
        {
            P("При стартиране приложението извежда страница за вход, която заема "
              + "цялата работна област. Всеки служител влиза със собствено "
              + "потребителско име и парола, а обхватът на достъпа се определя от "
              + "неговата роля."),
            H("Фармацевт"),
            P("Работата на гишето: продажби, приемане на доставки, търсене в "
              + "каталога, преглед на наличностите и партидите, бракуване на негодни "
              + "количества и оперативните справки за наличности и срокове. "
              + "Фармацевтът не вижда доставни цени, отчетна стойност, оборот и "
              + "печалба и не променя каталога."),
            H("Управител"),
            P("Всички права на фармацевта и допълнително: стойностните показатели на "
              + "склада, финансовите справки, регистърът на доставките, поддържането "
              + "на каталога, доставчиците и номенклатурите, анулирането на "
              + "приключени продажби и администрирането на системата."),
            N("След пет неуспешни опита за вход в рамките на десет минути профилът се "
              + "блокира временно. Всеки опит се записва в одитния дневник."),
            H("Смяна на собствената парола"),
            P("Извършва се от менюто на потребителя в горния десен ъгъл на прозореца. "
              + "Изисква въвеждане на текущата парола и двукратно въвеждане на новата."),
            H("Излизане от профила"),
            P("От същото меню. Приложението остава отворено и извежда отново "
              + "страницата за вход. За затваряне на приложението се използва бутонът "
              + "в горния десен ъгъл на прозореца или връзката в долната част на "
              + "страницата за вход."),
        }
        : new List<Block>
        {
            P("При стартиране приложението извежда страница за вход, която заема "
              + "цялата работна област. Влизате със собствено потребителско име и "
              + "парола."),
            H("Какво включва ролята „Фармацевт“"),
            B("продажби на каса и повторен печат на касова бележка;"),
            B("приемане и заприхождаване на доставки;"),
            B("търсене в каталога на лекарствените продукти;"),
            B("преглед на наличностите, партидите и складовите движения;"),
            B("бракуване на негодни и на изтекли количества;"),
            B("справки за складовата наличност и за изтичащите срокове."),
            P("Стойностните показатели на аптеката – доставни цени, отчетна стойност, "
              + "оборот и печалба – са достъпни само за управителя. За същото се "
              + "отнасят анулирането на приключена продажба, поддържането на каталога "
              + "и администрирането на системата."),
            N("След пет неуспешни опита за вход в рамките на десет минути профилът се "
              + "блокира временно. Всеки опит се записва в одитния дневник."),
            H("Смяна на собствената парола"),
            P("Извършва се от менюто на потребителя в горния десен ъгъл на прозореца. "
              + "Изисква въвеждане на текущата парола и двукратно въвеждане на новата."),
            H("Излизане от профила"),
            P("От същото меню. Приложението остава отворено и извежда отново "
              + "страницата за вход. За затваряне на приложението се използва бутонът "
              + "в горния десен ъгъл на прозореца или връзката в долната част на "
              + "страницата за вход."),
        };

    private static List<Block> Selling(bool isManager)
    {
        var blocks = new List<Block>
        {
            P("Касовият екран се отваря от раздел „Продажби“ в менюто вляво или с "
              + "клавишната комбинация Ctrl+N. Той е разделен на две части: търсене на "
              + "продукт вляво и сметка на клиента вдясно."),
            H("Ход на операцията"),
            S(1, "Въведете началото на наименованието, кода, баркода или активната "
                 + "съставка в полето за търсене. Списъкът се стеснява при всеки "
                 + "въведен знак."),
            S(2, "Изберете продукта и натиснете Enter или щракнете двукратно върху реда. "
                 + "Количеството се задава предварително в полето „Количество“."),
            S(3, "Проверете общата сума, изписана с едър шрифт под сметката. "
                 + "Тя се преизчислява автоматично при всяка промяна."),
            S(4, "Изберете начин на плащане и натиснете „Плащане“ или клавиш F9."),
            H("Цветово означение на редовете"),
            B("бял фон – наличността е достатъчна;"),
            B("жълт фон – наличността е под или равна на минималния запас;"),
            B("червен фон – продуктът е изчерпан и не може да бъде продаден."),
            H("Продукти по лекарско предписание"),
            P("Изписани са в курсив. При добавянето им системата изисква изрично "
              + "потвърждение, че е представена рецепта."),
            H("Как работи търсенето"),
            P("Съвпадението започва от началото на дума, а не от произволно място в "
              + "текста. При въведено „ана“ се намира „Аналгин“, но не и „Панадол“. "
              + "Търси се във всяка дума от наименованието, така че „сироп“ намира "
              + "„Парацетамол сироп за деца“. Големите и малките букви са "
              + "равнозначни."),
            N("Изписването се извършва по правилото „първи изтича – първи излиза“: "
              + "първо се продава партидата с най-близък срок на годност. Партиди с "
              + "изтекъл срок изобщо не участват в продажби."),
            H("Регистър на продажбите"),
        };

        blocks.Add(isManager
            ? P("Съдържа документите на всички служители с редовете им и позволява "
                + "повторен печат на касова бележка. Списъкът се стеснява по период и "
                + "по служител. Погрешно издаден документ се анулира с бутона "
                + "„Анулирай продажба“ – количествата се връщат в партидите, от които "
                + "са изписани, а действието се записва в одитния дневник.")
            : P("Съдържа Вашите документи за избрания период с редовете им и позволява "
                + "повторен печат на касова бележка. Документите на останалите "
                + "служители не се показват – регистърът служи за сверяване на касата "
                + "в собствената Ви смяна. Погрешно издаден документ се анулира от "
                + "управителя."));

        blocks.Add(N("Данните се обновяват сами при всяка промяна на периода или на "
                     + "останалите условия – не е необходимо отделно потвърждение."));

        return blocks;
    }

    private static List<Block> Delivery(bool isManager)
    {
        var blocks = new List<Block>
        {
            P("Екранът се отваря от раздел „Склад“ или с клавишната комбинация Ctrl+D. "
              + "Заприхождаването е ежедневна складова операция: стоката се приема от "
              + "служителя на смяна срещу фактурата на доставчика."),
            S(1, "Изберете доставчик, въведете номер на фактурата и датата на доставката."),
            S(2, "За всеки ред посочете продукт, партиден номер, срок на годност, "
                 + "количество и доставна цена от фактурата и натиснете „Добави ред“. "
                 + "Доставната цена е задължителна и трябва да е по-голяма от нула, "
                 + "защото определя себестойността на партидата."),
            S(3, "Сверете общата стойност под таблицата със сумата по фактурата."),
            S(4, "Натиснете „Заприходи доставката“."),
            N("Системата не допуска заприхождаване на партида с изтекъл срок или със "
              + "срок, съвпадащ с датата на доставката."),
            P("Ако партида със същия номер и срок вече съществува, количеството ѝ се "
              + "увеличава и се актуализира доставната цена. В противен случай се "
              + "създава нова партида."),
        };

        blocks.Add(isManager
            ? P("Заприходените документи се преглеждат в „Регистър на доставките“ – по "
                + "период и по доставчик, със стойност на всеки документ и с редовете му.")
            : P("Всеки документ пази кой служител го е заприходил. Обобщеният регистър "
                + "на доставките със стойностите по документи се преглежда от "
                + "управителя."));

        return blocks;
    }

    private static List<Block> Warehouse(bool isManager)
    {
        var blocks = new List<Block>
        {
            P("Екранът „Наличности и партиди“ извежда всички налични партиди, "
              + "подредени по нарастващ срок на годност."),
            H("Кога се извеждат предупрежденията"),
            B("при стартиране на приложението – върху началния екран и в лентата на състоянието;"),
            B("при отваряне на екрана „Наличности и партиди“ – като обобщено съобщение."),
            H("Видове предупреждения"),
            B("продукти с наличност под или равна на минималния запас;"),
            B("партиди, чийто срок изтича в рамките на зададения праг;"),
            B("партиди с вече изтекъл срок на годност."),
            H("Бракуване"),
        };

        blocks.Add(isManager
            ? P("Партидите с изтекъл срок се бракуват с бутона „Бракувай всички изтекли“ "
                + "или поединично с „Бракувай избраната партида“. Съставя се протокол с "
                + "основание и отчетна стойност по доставни цени.")
            : P("Партидите с изтекъл срок се бракуват с бутона „Бракувай всички изтекли“ "
                + "или поединично с „Бракувай избраната партида“. Съставя се протокол с "
                + "основание и с бракуваните количества."));

        blocks.Add(H("Складови движения"));
        blocks.Add(P("Всяко изменение на наличността – доставка, продажба, анулиране или "
                     + "брак – оставя запис в складовия журнал. Историята на една партида "
                     + "се вижда с бутона „Движения по партидата“."));

        blocks.Add(isManager
            ? N("Прагът за предупреждение при изтичащ срок е шестдесет дни по "
                + "подразбиране и се променя в „Администрация → Системни настройки“.")
            : N("Прагът за предупреждение при изтичащ срок е шестдесет дни по "
                + "подразбиране и се задава от управителя."));

        return blocks;
    }

    private static List<Block> Catalog(bool isManager) => isManager
        ? new List<Block>
        {
            P("Каталогът на лекарствените продукти се отваря от раздел „Номенклатури“ "
              + "или с Ctrl+M. Поддържа търсене по свободен текст и филтриране по "
              + "активна съставка, лекарствена форма и доставчик."),
            H("Правила при въвеждане на продукт"),
            B("кодът е осемразряден и съдържа само цифри;"),
            B("баркодът съдържа между 8 и 14 цифри и е незадължителен;"),
            B("цената и минималната наличност не могат да бъдат отрицателни;"),
            B("кодът и баркодът са уникални в рамките на каталога."),
            H("Премахване на продукт"),
            P("Ако за продукта има складови документи, той не се изтрива, а само се "
              + "спира от продажба. Така историята на операциите се запазва."),
            H("Доставчици и номенклатури"),
            P("В същия раздел се поддържат данните на доставчиците, активните съставки "
              + "и лекарствените форми. Доставчик с регистрирани доставки не се "
              + "изтрива, а се деактивира."),
            N("Поддържането на каталога определя асортимента и продажните цени и "
              + "затова е достъпно само за роля „Управител“."),
        }
        : new List<Block>
        {
            P("Каталогът на лекарствените продукти се отваря от раздел „Номенклатури“ "
              + "или с Ctrl+M и служи за справка на гишето."),
            H("Търсене"),
            B("начало на дума от наименованието, кода, баркода, активната съставка "
              + "или производителя – „ана“ намира „Аналгин“, но не и „Панадол“;"),
            B("филтриране по активна съставка, лекарствена форма и доставчик;"),
            B("отметка „Само активни“ скрива спрените от продажба продукти."),
            H("Какво показва списъкът"),
            P("Наименование, активна съставка, лекарствена форма, дозировка, "
              + "производител, продажна цена, налично количество, минимален запас и "
              + "най-близкия срок на годност. Редовете с недостатъчна наличност са "
              + "оцветени в жълто, а изчерпаните – в червено."),
            H("Партиди на продукта"),
            P("Бутонът „Партиди на продукта“ – или двукратно щракване върху ред – "
              + "показва партидите на избрания продукт с количествата и сроковете им."),
            N("Данните в каталога се поддържат от управителя. Ако установите "
              + "несъответствие в цена или в минимален запас, уведомете го."),
        };

    private static List<Block> Reports(bool isManager)
    {
        var blocks = new List<Block>
        {
            P("Всяка справка се изготвя за избран период или параметър и може да бъде "
              + "изнесена във файл във формат CSV, който се отваря непосредствено в "
              + "Microsoft Excel или LibreOffice Calc."),
            N("Справката се преизчислява веднага след всяка промяна на параметрите. "
              + "Ако началната дата бъде избрана след крайната, системата премества "
              + "крайната след нея, за да остане периодът смислен."),
        };

        if (isManager)
        {
            blocks.Add(H("Оперативни справки"));
            blocks.Add(P("Обслужват ежедневната работа и са достъпни за всички служители."));
            blocks.Add(B("Складова наличност – количества, минимални запаси и отчетна стойност;"));
            blocks.Add(B("Изтичащи срокове на годност – партиди по остатъчен срок."));
            blocks.Add(H("Управленски справки"));
            blocks.Add(P("Показват стойностните резултати на аптеката и са достъпни "
                         + "само за роля „Управител“."));
            blocks.Add(B("Дневен оборот – продажби, оборот, себестойност и печалба по дни;"));
            blocks.Add(B("Най-продавани продукти – класация по количество и оборот;"));
            blocks.Add(B("Продажби по служители – оборот и среден размер на покупката;"));
            blocks.Add(B("Доставки по доставчици – доставени количества и стойност;"));
            blocks.Add(B("Бракувани количества – брак по основания и стойност."));
            blocks.Add(N("Опит за отваряне на управленска справка от профил без права "
                         + "се отхвърля от слоя с бизнес логика, а не само от "
                         + "интерфейса."));
        }
        else
        {
            blocks.Add(H("Складова наличност"));
            blocks.Add(P("Извежда всички продукти с наличното количество, минималния "
                         + "запас, продажната цена и най-близкия срок на годност. "
                         + "Отметката „Само под минимума“ оставя в списъка единствено "
                         + "продуктите за поръчка."));
            blocks.Add(H("Изтичащи срокове на годност"));
            blocks.Add(P("Извежда партидите, чийто срок изтича в рамките на зададения "
                         + "брой дни, подредени по остатъчен срок. Служи за "
                         + "своевременно изтегляне на застрашените количества."));
            blocks.Add(N("Справките за оборот, печалба и класации на продуктите са "
                         + "управленски и се изготвят от управителя."));
        }

        return blocks;
    }

    private static List<Block> Administration() => new()
    {
        P("Разделът е достъпен само за потребители с роля „Управител“."),
        H("Потребители"),
        P("Създаване на нови профили, промяна на данните и на ролята, деактивиране и "
          + "задаване на нова парола. Паролата се въвежда два пъти за потвърждение и "
          + "трябва да е дълга поне осем знака и да съдържа поне една буква и поне "
          + "една цифра."),
        N("Системата не позволява да остане без нито един активен управител."),
        H("Системни настройки"),
        P("Поддържат се данните за търговския обект – наименование и адрес на "
          + "аптеката и ставка на ДДС. Наименованието се извежда в заглавната лента, "
          + "а наименованието, адресът и данъкът се отпечатват върху касовата "
          + "бележка. Тук се задава и прагът в дни, при който системата "
          + "предупреждава за изтичащ срок на годност."),
        H("Одитен дневник"),
        P("Съдържа записи за входовете в системата, неуспешните опити, смените на "
          + "пароли, промените по профилите и анулираните продажби."),
        H("Контрол на наличностите"),
        P("Сравнява текущото количество по всяка партида със сумата от записите в "
          + "складовия журнал. При изправна работа резултатът е празен списък."),
    };

    private static List<Block> Shortcuts(bool isManager)
    {
        var blocks = new List<Block>
        {
            P("Клавишните комбинации действат от всяка страница на приложението."),
            H("Навигация"),
            K("Ctrl + H", "Начална страница"),
            K("Ctrl + N", "Нова продажба"),
            K("Ctrl + R", "Регистър на продажбите"),
            K("Ctrl + S", "Складови наличности"),
            K("Ctrl + D", "Нова доставка"),
            K("Ctrl + M", "Каталог на продуктите"),
            K("F1", "Това ръководство"),
            H("Касов екран"),
            K("F2", "Връщане на фокуса в полето за търсене"),
            K("Enter", "Добавяне на избрания продукт в сметката"),
            K("Delete", "Премахване на избрания ред от сметката"),
            K("F9", "Приключване на продажбата"),
        };

        return blocks;
    }
}

/// <summary>Данни за програмата и за нейното предназначение.</summary>
public class AboutForm : Form
{
    public AboutForm()
    {
        UiHelpers.StyleDialog(this, "За програмата", 560, 470);

        var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0";

        var layout = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            Padding = new Padding(24, 18, 24, 18),
            BackColor = Color.White
        };

        void Title(string text) => layout.Controls.Add(new Label
        {
            Text = text,
            Font = UiHelpers.FontSection,
            ForeColor = UiHelpers.Ink,
            AutoSize = true,
            Margin = new Padding(0, 12, 0, 6)
        });

        void Text(string text) => layout.Controls.Add(new Label
        {
            Text = text,
            Font = UiHelpers.FontBody,
            ForeColor = UiHelpers.Ink,
            MaximumSize = new Size(480, 0),
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 4)
        });

        Text($"Информационна система за аптека, версия {version}");
        Text("Курсов проект по дисциплина „Информационни системи“, задание № 4");
        Text("Тракийски университет – Стара Загора, Стопански факултет");

        Title("Предназначение");
        Text("Системата поддържа каталог на лекарствените продукти, складовите "
             + "наличности по партиди, продажбите и доставките. Издава предупреждения "
             + "при ниска наличност и при изтичащ срок на годност и извежда справки "
             + "за дейността на аптеката.");

        Title("Използвани технологии");
        Text("• Език за програмиране: C# 13, платформа .NET 9\n"
             + "• Потребителски интерфейс: Windows Forms\n"
             + "• Достъп до данни: ADO.NET\n"
             + $"• База от данни: {AppSession.Sessions.Settings.Provider}\n"
             + "• Автоматизирани тестове: xUnit");

        Title("Архитектура");
        Text("Слоеста: областен модел, слой за достъп до данни, слой с бизнес логика "
             + "и слой за представяне. Зависимостите между слоевете са еднопосочни.");

        var close = UiHelpers.CreateButton("Затвори", 120, primary: true);
        close.Click += (_, _) => Close();

        Controls.Add(layout);
        Controls.Add(UiHelpers.DialogButtons(close));
        Controls.Add(UiHelpers.CreateHeader("Информационна система за аптека",
            "Курсов проект по „Информационни системи“, вариант 4"));

        AcceptButton = close;
        CancelButton = close;
    }
}
