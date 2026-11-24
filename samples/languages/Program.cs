using DinkToPdf;

// The languages picture on the package's page: dotnet run, then open languages.pdf.
// Letters are drawn with the fonts installed on the computer (chapter 8 of the manual).

var thanks = new (string Language, string Text, string Direction)[]
{
    ("English", "Thank you for your order", "ltr"),
    ("Arabic", "شكرا لطلبك", "rtl"),
    ("Hebrew", "תודה על ההזמנה", "rtl"),
    ("Hindi", "आपके ऑर्डर के लिए धन्यवाद", "ltr"),
    ("Thai", "ขอบคุณสำหรับคำสั่งซื้อ", "ltr"),
    ("Chinese", "感谢您的订购", "ltr"),
    ("Japanese", "ご注文ありがとうございます", "ltr"),
    ("Korean", "주문해 주셔서 감사합니다", "ltr"),
};

string cards = "";
foreach (var (language, text, direction) in thanks)
{
    cards += $"<tr><th>{language}</th><td dir=\"{direction}\">{text}</td></tr>";
}

string html = $$"""
    <!DOCTYPE html>
    <html>
    <head>
      <meta charset="utf-8">
      <style>
        body { font-family: "Helvetica Neue", Helvetica, Arial, sans-serif; color: #1d2433; margin: 0; }
        h1 { font-size: 22px; font-weight: 300; color: #0b5cad; margin: 0 0 18px 0; }
        table { width: 100%; border-collapse: collapse; }
        th { width: 22%; text-align: left; font-size: 11px; letter-spacing: 2px; text-transform: uppercase; color: #6b7487; font-weight: normal; padding: 14px 10px; border-bottom: 1px solid #e3e8f0; }
        td { font-size: 24px; padding: 14px 10px; border-bottom: 1px solid #e3e8f0; }
        td[dir=rtl] { text-align: right; }
      </style>
    </head>
    <body>
      <h1>One page, eight languages, real text you can search and copy</h1>
      <table>{{cards}}</table>
    </body>
    </html>
    """;

var converter = new SynchronizedConverter(new PdfTools());
converter.Warning += (sender, e) => Console.WriteLine("Warning: " + e.Message);
var document = new HtmlToPdfDocument
{
    GlobalSettings = { PaperSize = PaperKind.A4, Margins = new MarginSettings(16, 16, 16, 16) },
    Objects = { new ObjectSettings { HtmlContent = html } },
};

File.WriteAllBytes("languages.pdf", converter.Convert(document));
Console.WriteLine("languages.pdf is ready");
