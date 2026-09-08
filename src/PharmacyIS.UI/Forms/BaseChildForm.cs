using PharmacyIS.UI.Infrastructure;

namespace PharmacyIS.UI.Forms;

/// <summary>
/// Обща основа за работните страници на приложението.
///
/// Страниците не са самостоятелни прозорци: те се вграждат в работната
/// област на главния прозорец. Затова нямат собствена рамка и системни
/// бутони – в приложението има един-единствен прозорец.
/// </summary>
public class BaseChildForm : Form
{
    /// <summary>Наименование на страницата, изписвано в заглавната ѝ лента.</summary>
    public string PageTitle { get; }

    /// <summary>Лента с бутони за действие под заглавието.</summary>
    protected FlowLayoutPanel Toolbar { get; } = UiHelpers.CreateToolbar();

    /// <summary>Основна работна област на страницата.</summary>
    protected Panel Content { get; } = new()
    {
        Dock = DockStyle.Fill,
        Padding = new Padding(16, 12, 16, 12),
        BackColor = Color.White
    };

    protected BaseChildForm(string title, string subtitle)
    {
        PageTitle = title;

        Text = title;
        // Страницата се показва вградена в главния прозорец, а не като
        // самостоятелен прозорец с рамка и системни бутони.
        FormBorderStyle = FormBorderStyle.None;
        TopLevel = false;
        ControlBox = false;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowInTaskbar = false;
        Dock = DockStyle.Fill;
        BackColor = Color.White;
        Font = UiHelpers.FontBody;

        Controls.Add(Content);
        Controls.Add(UiHelpers.Separator());
        Controls.Add(Toolbar);
        Controls.Add(UiHelpers.CreatePageHeader(title, subtitle));
    }

    /// <summary>
    /// Извиква се всеки път, когато страницата се извежда на преден план.
    /// Наследниците презаписват метода, за да обновят данните си.
    /// </summary>
    public virtual void OnPageActivated()
    {
    }

    /// <summary>Добавя бутон към лентата с действия.</summary>
    protected Button AddToolbarButton(string text, Action action, int minWidth = 140,
        bool primary = false)
    {
        var button = UiHelpers.CreateButton(text, minWidth, primary);
        button.Click += (_, _) => Run(action);
        Toolbar.Controls.Add(button);
        return button;
    }

    /// <summary>Добавя отстояние между групите бутони.</summary>
    protected void AddToolbarSeparator()
        => Toolbar.Controls.Add(new Panel
        {
            Width = 1,
            Height = 26,
            Margin = new Padding(8, 3, 16, 3),
            BackColor = UiHelpers.Border
        });

    /// <summary>
    /// Изпълнява действие, като прехваща и показва евентуалната грешка,
    /// вместо приложението да прекъсва работа.
    /// </summary>
    protected void Run(Action action)
    {
        try
        {
            Cursor = Cursors.WaitCursor;
            action();
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
}
