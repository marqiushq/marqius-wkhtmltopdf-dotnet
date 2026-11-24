using System.Globalization;
using System.Net;
using DinkToPdf;

// The invoice on the package's page: dotnet run, then open invoice.pdf

string folder = Path.Combine(AppContext.BaseDirectory, "templates");
var pounds = CultureInfo.GetCultureInfo("en-GB");

var lines = new (string Item, int Quantity, decimal Price)[]
{
    ("A4 copier paper, 80 gsm, box of 5 reams", 12, 24.99m),
    ("Laser toner cartridge, black, high yield", 3, 79.00m),
    ("Desk organiser, recycled steel", 5, 18.50m),
    ("Next-day delivery", 1, 9.95m),
};

string rows = "";
foreach (var line in lines)
{
    rows += $"<tr><td>{WebUtility.HtmlEncode(line.Item)}</td>"
          + $"<td class=\"num\">{line.Quantity}</td>"
          + $"<td class=\"num\">{line.Price.ToString("C", pounds)}</td>"
          + $"<td class=\"num\">{(line.Quantity * line.Price).ToString("C", pounds)}</td></tr>";
}

decimal subtotal = lines.Sum(line => line.Quantity * line.Price);
decimal vat = Math.Round(subtotal * 0.20m, 2);

string html = File.ReadAllText(Path.Combine(folder, "invoice.html"))
    .Replace("{{folder}}", new Uri(folder + Path.DirectorySeparatorChar).AbsoluteUri)
    .Replace("{{number}}", "INV-2026-0042")
    .Replace("{{date}}", "24 November 2026")
    .Replace("{{due}}", "24 December 2026")
    .Replace("{{rows}}", rows)
    .Replace("{{subtotal}}", subtotal.ToString("C", pounds))
    .Replace("{{vat}}", vat.ToString("C", pounds))
    .Replace("{{total}}", (subtotal + vat).ToString("C", pounds));

var converter = new SynchronizedConverter(new PdfTools());
var document = new HtmlToPdfDocument
{
    GlobalSettings =
    {
        PaperSize = PaperKind.A4,
        Margins = new MarginSettings(16, 16, 20, 16),
        DocumentTitle = "Invoice INV-2026-0042",
    },
    Objects =
    {
        new ObjectSettings
        {
            HtmlContent = html,
            FooterSettings = { Left = "Northwind Supplies Ltd · 12 Harbour Street, Bristol BS1 4QA", Right = "Page [page] of [toPage]", FontSize = 7 },
        },
    },
};

File.WriteAllBytes("invoice.pdf", converter.Convert(document));
Console.WriteLine("invoice.pdf is ready");
