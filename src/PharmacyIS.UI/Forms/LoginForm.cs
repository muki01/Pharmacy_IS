using PharmacyIS.UI.Infrastructure;

namespace PharmacyIS.UI.Forms;

/// <summary>
/// Страница за вход в системата. Заема цялата работна област на главния
/// прозорец, а не се извежда като отделен диалогов прозорец – приложението
/// има един-единствен прозорец през целия си жизнен цикъл.
/// </summary>
public class LoginPage : Form
{
    private readonly Action _onSuccess;
    private readonly Action _onExit;

    private readonly TextBox _username = new();
    private readonly TextBox _password = new();
    private readonly Button _loginButton = UiHelpers.CreateButton("Вход в системата", 300, primary: true);
    private readonly LinkLabel _exitLink = new();
    private readonly Label _errorLabel = new();

    public LoginPage(Action onSuccess, Action onExit)
    {
        _onSuccess = onSuccess;
        _onExit = onExit;

        FormBorderStyle = FormBorderStyle.None;
        TopLevel = false;
        ControlBox = false;
        Dock = DockStyle.Fill;
        BackColor = Color.White;
        Font = UiHelpers.FontBody;

        BuildLayout();

        AcceptButton = _loginButton;
        Shown += (_, _) => _username.Focus();
    }

    /// <summary>Поставя фокуса в първото поле при връщане към страницата.</summary>
    public void Reset()
    {
        _username.Clear();
        _password.Clear();
        _errorLabel.Text = string.Empty;
        _username.Focus();
    }

    // ------------------------------------------------------------------
    //  Изграждане
    // ------------------------------------------------------------------

    private void BuildLayout()
    {
        // Централиране на картата с формата: празни редове и колони с
        // пропорционален размер от двете ѝ страни.
        var centering = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            BackColor = Color.White
        };

        centering.RowStyles.Add(new RowStyle(SizeType.Percent, 42));
        centering.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        centering.RowStyles.Add(new RowStyle(SizeType.Percent, 58));

        centering.Controls.Add(new Panel { Margin = new Padding(0), Height = 0 }, 0, 0);
        centering.Controls.Add(BuildCard(), 0, 1);
        centering.Controls.Add(BuildFooter(), 0, 2);

        Controls.Add(centering);
    }

    private Control BuildCard()
    {
        var card = new TableLayoutPanel
        {
            ColumnCount = 1,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Anchor = AnchorStyles.None,
            Padding = new Padding(40, 32, 40, 32),
            BackColor = Color.White,
            Margin = new Padding(0)
        };

        // Тънка рамка и цветна ивица отгоре очертават картата.
        card.Paint += (s, e) =>
        {
            var c = (Control)s!;
            using var pen = new Pen(UiHelpers.Border);
            e.Graphics.DrawRectangle(pen, 0, 0, c.Width - 1, c.Height - 1);
            using var accent = new SolidBrush(UiHelpers.Primary);
            e.Graphics.FillRectangle(accent, 0, 0, c.Width, 4);
        };

        card.Controls.Add(new Label
        {
            Text = "Вход в системата",
            Font = new Font("Segoe UI Semibold", 17f),
            ForeColor = UiHelpers.Ink,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 4)
        });

        card.Controls.Add(new Label
        {
            Text = "Въведете потребителско име и парола, за да продължите.",
            Font = UiHelpers.FontBody,
            ForeColor = UiHelpers.Muted,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 24)
        });

        _username.Width = 430;
        _username.Font = new Font("Segoe UI", 11f);
        _username.Margin = new Padding(0, 0, 0, 16);

        _password.Width = 430;
        _password.Font = new Font("Segoe UI", 11f);
        _password.UseSystemPasswordChar = true;
        _password.Margin = new Padding(0, 0, 0, 6);

        card.Controls.Add(Caption("Потребителско име"));
        card.Controls.Add(_username);
        card.Controls.Add(Caption("Парола"));
        card.Controls.Add(_password);

        _errorLabel.ForeColor = Color.Firebrick;
        _errorLabel.Font = UiHelpers.FontBody;
        _errorLabel.AutoSize = false;
        _errorLabel.Width = 430;
        _errorLabel.Height = 38;
        _errorLabel.Margin = new Padding(0, 2, 0, 6);
        card.Controls.Add(_errorLabel);

        _loginButton.Width = 430;
        _loginButton.MinimumSize = new Size(430, 42);
        _loginButton.Margin = new Padding(0, 0, 0, 18);
        _loginButton.Click += OnLogin;
        card.Controls.Add(_loginButton);

        card.Controls.Add(new Panel
        {
            Width = 430,
            Height = 1,
            BackColor = UiHelpers.Border,
            Margin = new Padding(0, 0, 0, 14)
        });

        card.Controls.Add(new Label
        {
            Text = "Демонстрационни профили\n"
                   + "управител:  manager / manager123\n"
                   + "фармацевт:  farmacevt / farmacevt123",
            Font = UiHelpers.FontSmall,
            ForeColor = UiHelpers.Muted,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 12)
        });

        _exitLink.Text = "Затваряне на приложението";
        _exitLink.Font = UiHelpers.FontSmall;
        _exitLink.LinkColor = UiHelpers.Muted;
        _exitLink.ActiveLinkColor = UiHelpers.Primary;
        _exitLink.LinkBehavior = LinkBehavior.HoverUnderline;
        _exitLink.AutoSize = true;
        _exitLink.Margin = new Padding(0);
        _exitLink.LinkClicked += (_, _) => _onExit();
        card.Controls.Add(_exitLink);

        return card;
    }

    private static Control BuildFooter()
    {
        return new Label
        {
            Text = "Тракийски университет – Стара Загора  ·  Стопански факултет\n"
                   + "Курсов проект по дисциплина „Информационни системи“",
            Font = UiHelpers.FontSmall,
            ForeColor = UiHelpers.Muted,
            TextAlign = ContentAlignment.TopCenter,
            Dock = DockStyle.Fill,
            Padding = new Padding(0, 26, 0, 0),
            Margin = new Padding(0)
        };
    }

    private static Label Caption(string text) => new()
    {
        Text = text,
        AutoSize = true,
        Font = UiHelpers.FontBodyBold,
        ForeColor = UiHelpers.Ink,
        Margin = new Padding(0, 0, 0, 5)
    };

    // ------------------------------------------------------------------
    //  Удостоверяване
    // ------------------------------------------------------------------

    private void OnLogin(object? sender, EventArgs e)
    {
        _errorLabel.Text = string.Empty;
        _loginButton.Enabled = false;
        Cursor = Cursors.WaitCursor;

        try
        {
            var user = AppSession.Auth.Login(_username.Text, _password.Text);
            AppSession.SignIn(user);
            _onSuccess();
        }
        catch (Exception ex)
        {
            _errorLabel.Text = ex.Message;
            _password.Clear();
            _password.Focus();
        }
        finally
        {
            _loginButton.Enabled = true;
            Cursor = Cursors.Default;
        }
    }
}
