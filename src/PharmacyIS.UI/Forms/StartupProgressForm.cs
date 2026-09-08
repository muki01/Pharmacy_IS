using PharmacyIS.UI.Infrastructure;

namespace PharmacyIS.UI.Forms;

/// <summary>
/// Показва напредъка на продължителна начална операция, докато тя се
/// изпълнява във фонова нишка. Така прозорецът остава отзивчив.
/// </summary>
public class StartupProgressForm : Form
{
    /// <summary>Грешката, възникнала при изпълнението, ако има такава.</summary>
    public Exception? Error { get; private set; }

    private readonly Action _work;

    public StartupProgressForm(string title, string message, Action work)
    {
        _work = work;

        UiHelpers.StyleDialog(this, title, 470, 180);
        StartPosition = FormStartPosition.CenterScreen;
        ControlBox = false;

        var label = new Label
        {
            Text = message,
            Dock = DockStyle.Top,
            Height = 60,
            Padding = new Padding(16, 16, 16, 0),
            Font = new Font("Segoe UI", 9.75f)
        };

        var bar = new ProgressBar
        {
            Dock = DockStyle.Top,
            Height = 22,
            Style = ProgressBarStyle.Marquee,
            MarqueeAnimationSpeed = 30,
            Margin = new Padding(16)
        };

        var container = new Panel { Dock = DockStyle.Fill, Padding = new Padding(16) };
        container.Controls.Add(bar);
        container.Controls.Add(label);

        Controls.Add(container);
        Controls.Add(UiHelpers.CreateHeader("Информационна система за аптека", "Първоначална подготовка"));

        Shown += OnShown;
    }

    private async void OnShown(object? sender, EventArgs e)
    {
        try
        {
            await Task.Run(_work);
        }
        catch (Exception ex)
        {
            Error = ex;
        }
        finally
        {
            Close();
        }
    }
}
