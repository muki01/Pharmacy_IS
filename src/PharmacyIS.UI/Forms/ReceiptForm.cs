using PharmacyIS.Domain;
using System.Drawing.Printing;
using System.Text;
using PharmacyIS.Domain.Entities;
using PharmacyIS.UI.Infrastructure;

namespace PharmacyIS.UI.Forms;

/// <summary>
/// Показва касовата бележка за току-що приключена продажба
/// и позволява отпечатването ѝ.
/// </summary>
public class ReceiptForm : Form
{
    private readonly Sale _sale;
    private readonly TextBox _text = new();

    public ReceiptForm(Sale sale)
    {
        _sale = sale;

        UiHelpers.StyleDialog(this, $"Касова бележка № {sale.DocNumber}", 600, 650);

        _text.Multiline = true;
        _text.ReadOnly = true;

        // Бележката е форматирана с равноширок шрифт и не се пренася, за да
        // се запази подравняването на колоните със сумите. Прозорецът е
        // достатъчно широк за реда от 46 знака, затова е нужна само
        // вертикална лента за превъртане при по-дълги документи.
        _text.WordWrap = false;
        _text.ScrollBars = ScrollBars.Vertical;
        _text.TabStop = false;
        _text.Font = new Font("Consolas", 9.5f);
        _text.Dock = DockStyle.Fill;
        _text.BackColor = Color.White;
        _text.BorderStyle = BorderStyle.None;
        _text.Text = BuildReceiptText();

        var close = UiHelpers.CreateButton("Затвори", 120, primary: true);
        close.Click += (_, _) => Close();

        var print = UiHelpers.CreateButton("Отпечатай", 120);
        print.Click += (_, _) => PrintReceipt();

        var content = new Panel { Dock = DockStyle.Fill, Padding = new Padding(16) };
        content.Controls.Add(_text);

        Controls.Add(content);
        Controls.Add(UiHelpers.DialogButtons(close, print));
        Controls.Add(UiHelpers.CreateHeader("Продажбата е записана",
            $"Документ № {sale.DocNumber} · {sale.SaleDate:dd.MM.yyyy HH:mm}"));

        AcceptButton = close;

        // Фокусът се насочва към бутона, за да не изглежда бележката маркирана.
        Shown += (_, _) => { _text.SelectionLength = 0; close.Focus(); };
    }

    /// <summary>Съставя текста на касовата бележка.</summary>
    private string BuildReceiptText()
    {
        var builder = new StringBuilder();
        const int width = 46;

        builder.AppendLine(Center(AppSession.Pharmacy.Name.ToUpperInvariant(), width));

        // Адресът на обекта е част от изискуемите реквизити на документа.
        foreach (var line in WrapAddress(AppSession.Pharmacy.Address, width))
            builder.AppendLine(Center(line, width));

        builder.AppendLine(Center("КАСОВА БЕЛЕЖКА", width));
        builder.AppendLine(new string('=', width));
        builder.AppendLine($"Документ №: {_sale.DocNumber}");
        builder.AppendLine($"Дата:       {_sale.SaleDate:dd.MM.yyyy HH:mm}");
        builder.AppendLine($"Оператор:   {_sale.UserName}");
        builder.AppendLine(new string('-', width));

        foreach (var group in _sale.Items.GroupBy(i => new { i.MedicineCode, i.MedicineName, i.UnitPrice }))
        {
            var quantity = group.Sum(i => i.Quantity);
            var lineTotal = group.Sum(i => i.LineTotal);

            builder.AppendLine(group.Key.MedicineName);
            builder.AppendLine(
                $"   {quantity,4} бр. x {group.Key.UnitPrice,7:N2}{lineTotal,14:N2} {Money.Currency}");
        }

        builder.AppendLine(new string('-', width));
        builder.AppendLine($"{"ОБЩО:",-27}{_sale.TotalAmount,12:N2} {Money.Currency}");

        // Данъчната основа и данъкът се извеждат отделно: обявената цена
        // включва данъка, затова той се изчислява „отвътре“.
        var vatRate = AppSession.Pharmacy.VatRate;
        if (vatRate > 0)
        {
            var vat = Math.Round(_sale.TotalAmount * vatRate / (100m + vatRate),
                2, MidpointRounding.AwayFromZero);

            builder.AppendLine($"{"Данъчна основа:",-27}{_sale.TotalAmount - vat,12:N2} {Money.Currency}");
            builder.AppendLine($"{$"в т.ч. ДДС {vatRate}%:",-27}{vat,12:N2} {Money.Currency}");
        }

        builder.AppendLine($"{"Плащане:",-27}" +
            $"{(_sale.PaymentType == Domain.Enums.PaymentType.Cash ? "в брой" : "с карта"),16}");
        builder.AppendLine(new string('=', width));
        builder.AppendLine();
        builder.AppendLine(Center("Благодарим Ви!", width));
        builder.AppendLine(Center("Пазете документа за гаранция и рекламации.", width));

        return builder.ToString();
    }

    private static string Center(string text, int width)
        => text.Length >= width ? text : text.PadLeft((width + text.Length) / 2).PadRight(width);

    /// <summary>
    /// Разделя адреса на редове, които се събират в ширината на бележката.
    /// </summary>
    private static IEnumerable<string> WrapAddress(string address, int width)
    {
        if (string.IsNullOrWhiteSpace(address)) yield break;

        var line = string.Empty;

        foreach (var word in address.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (line.Length == 0)
            {
                line = word;
            }
            else if (line.Length + 1 + word.Length <= width)
            {
                line += " " + word;
            }
            else
            {
                yield return line;
                line = word;
            }
        }

        if (line.Length > 0) yield return line;
    }

    /// <summary>Отпечатва бележката на избран от потребителя принтер.</summary>
    private void PrintReceipt()
    {
        try
        {
            using var document = new PrintDocument();
            document.PrintPage += (_, e) =>
            {
                using var font = new Font("Consolas", 9.5f);
                e.Graphics?.DrawString(_text.Text, font, Brushes.Black,
                    e.MarginBounds.Left, e.MarginBounds.Top);
            };

            using var dialog = new PrintDialog { Document = document };
            if (dialog.ShowDialog(this) == DialogResult.OK)
                document.Print();
        }
        catch (Exception ex)
        {
            UiHelpers.ShowError(this, ex);
        }
    }
}
