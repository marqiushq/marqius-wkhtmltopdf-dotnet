using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DinkToPdf;

// The documents both sides make: DinkToPdf with libwkhtmltox 0.12.4, and Marqius.DinkToPdf. Together
// they set every setting of DinkToPdf's GlobalSettings, ObjectSettings, WebSettings, HeaderSettings,
// FooterSettings and LoadSettings that changes a PDF (cookies, custom headers, a proxy and passwords
// need a server, and have their own tests). Nothing depends on the date or the machine's name.
public static class Cases
{
    private const string Text = "<p>Lorem ipsum dolor sit amet, consectetur adipiscing elit. In consectetur mauris eget ultrices iaculis. Ut et odio viverra, molestie lectus nec, venenatis turpis. Nulla quis euismod nisl. Duis scelerisque eros nec dui facilisis, sit amet porta odio varius.</p>";

    private static string Body(string inner) => "<!DOCTYPE html><html><head><meta charset=\"utf-8\"><style>body{font-family:'Liberation Sans',Arial,sans-serif;font-size:12pt}</style></head><body>" + inner + "</body></html>";

    /// <summary>The files some documents link: written once into the work folder.</summary>
    public static void Files(string folder)
    {
        File.WriteAllBytes(Path.Combine(folder, "red.png"), Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAIAAAACCAIAAAD91JpzAAAAEElEQVR4nGP4z8AARAwQCgAf7gP9i18U1AAAAABJRU5ErkJggg=="));
        File.WriteAllText(Path.Combine(folder, "print.css"), "p { color: #003399; } h1 { border-bottom: 2px solid #990000; }");
        File.WriteAllText(Path.Combine(folder, "header.html"), Body(
            "<div style=\"text-align:right;font-size:9pt\">Header from a file: page <span class=\"page\"></span> of <span class=\"topage\"></span></div>" +
            "<script>var v={};var q=document.location.search.substring(1).split('&');for(var i in q){var z=q[i].split('=',2);v[z[0]]=decodeURIComponent(z[1]);}" +
            "var c=['page','topage'];for(var i in c){var y=document.getElementsByClassName(c[i]);for(var j=0;j<y.length;++j)y[j].textContent=v[c[i]];}</script>"));
        File.WriteAllText(Path.Combine(folder, "page.html"), Body("<h1>A page from a file</h1>" + Text));
    }

    public static IEnumerable<(string Name, HtmlToPdfDocument Document)> All(string folder)
    {
        string uri(string file) => new Uri(Path.Combine(folder, file)).AbsoluteUri;
        string Long(int n) { var s = ""; for (int i = 1; i <= n; i++) s += $"<h2>Part {i}</h2>" + Text + Text; return s; }

        HtmlToPdfDocument Doc(string html, Action<GlobalSettings> global = null, Action<ObjectSettings> obj = null)
        {
            var o = new ObjectSettings { HtmlContent = Body(html) };
            obj?.Invoke(o);
            var d = new HtmlToPdfDocument();
            global?.Invoke(d.GlobalSettings);
            d.Objects.Add(o);
            return d;
        }

        yield return ("01-default", Doc("<h1>Invoice 42</h1>" + Text));
        yield return ("02-landscape-a4", Doc("<h1>Landscape</h1>" + Text, g => { g.Orientation = Orientation.Landscape; g.PaperSize = PaperKind.A4; }));
        yield return ("03-letter-margins-inches", Doc("<h1>Letter</h1>" + Text, g => { g.PaperSize = PaperKind.Letter; g.Margins = new MarginSettings { Top = 1, Bottom = 1, Left = 0.75, Right = 0.75, Unit = Unit.Inches }; }));
        yield return ("04-a5-margins-mm", Doc("<h1>A5</h1>" + Text, g => { g.PaperSize = PaperKind.A5; g.Margins = new MarginSettings(5, 5, 5, 5); }));
        yield return ("05-legal-margins-cm", Doc("<h1>Legal</h1>" + Long(3), g => { g.PaperSize = PaperKind.Legal; g.Margins = new MarginSettings { Top = 2, Bottom = 2, Left = 1.5, Right = 1.5, Unit = Unit.Centimeters }; }));
        yield return ("06-grayscale", Doc("<h1 style=\"color:#c00\">Red heading</h1><p style=\"background:#0a0;color:#fff\">Green band</p>", g => g.ColorMode = ColorMode.Grayscale));
        yield return ("07-header-footer-text", Doc(Long(6), null, o =>
        {
            o.PagesCount = true;
            o.HeaderSettings = new HeaderSettings { FontSize = 9, FontName = "Liberation Sans", Left = "Invoice 42", Center = "Draft", Right = "Page [page] of [toPage]", Line = true, Spacing = 2.5 };
            o.FooterSettings = new FooterSettings { FontSize = 8, Center = "[page] / [topage]", Line = false, Spacing = 1 };
        }));
        yield return ("08-header-html-file", Doc(Long(5), g => g.Margins = new MarginSettings { Top = 20, Bottom = 10, Left = 10, Right = 10 }, o =>
        {
            o.PagesCount = true;
            o.HeaderSettings = new HeaderSettings { HtmUrl = uri("header.html"), Spacing = 3 };
        }));
        yield return ("09-no-background", Doc("<div style=\"background:#036;color:#fff;padding:20px\">Background colour</div>" + Text, null, o => o.WebSettings = new WebSettings { Background = false }));
        yield return ("10-no-images", Doc($"<p>Before</p><img src=\"{uri("red.png")}\" width=\"100\" height=\"100\"><p>After</p>", null, o => o.WebSettings = new WebSettings { LoadImages = false }));
        yield return ("11-javascript-on", Doc("<p id=\"x\">Script did not run</p><script>document.getElementById('x').textContent='Written by a script';</script>", null, o => o.WebSettings = new WebSettings { EnableJavascript = true }));
        yield return ("12-javascript-off", Doc("<p id=\"x\">Script did not run</p><script>document.getElementById('x').textContent='Written by a script';</script>", null, o => o.WebSettings = new WebSettings { EnableJavascript = false }));
        yield return ("13-print-media-type", Doc("<style>@media print{.screen{display:none}} @media screen{.print{display:none}}</style><p class=\"print\">Print styles</p><p class=\"screen\">Screen styles</p>", null, o => o.WebSettings = new WebSettings { PrintMediaType = true }));
        yield return ("14-minimum-font-size", Doc("<p style=\"font-size:4pt\">Very small type, made larger by the minimum font size.</p>", null, o => o.WebSettings = new WebSettings { MinimumFontSize = 14 }));
        yield return ("15-zoom", Doc("<h1>Zoomed</h1>" + Text, null, o => o.LoadSettings = new LoadSettings { ZoomFactor = 1.5 }));
        yield return ("16-user-style-sheet", Doc("<h1>Styled from a file</h1>" + Text, null, o => o.WebSettings = new WebSettings { UserStyleSheet = uri("print.css") }));
        yield return ("17-outline-title", Doc("<h1>Chapter one</h1>" + Text + "<h2>Section</h2>" + Text + "<h1 style=\"page-break-before:always\">Chapter two</h1>" + Text,
            g => { g.Outline = true; g.OutlineDepth = 2; g.DocumentTitle = "Outline and title"; }));
        yield return ("18-several-objects", new HtmlToPdfDocument
        {
            GlobalSettings = { PaperSize = PaperKind.A4 },
            Objects =
            {
                new ObjectSettings { HtmlContent = Body("<h1>First object</h1>" + Text) },
                new ObjectSettings { Page = uri("page.html") },
                new ObjectSettings { HtmlContent = Body("<h1>Third object</h1>" + Text), WebSettings = { Background = false } },
            },
        });
        yield return ("19-local-picture", Doc($"<p>A picture on this computer:</p><img src=\"{uri("red.png")}\" width=\"120\" height=\"120\">"));
        yield return ("20-local-picture-blocked", Doc($"<p>Blocked picture:</p><img src=\"{uri("red.png")}\" width=\"120\" height=\"120\">", null, o => o.LoadSettings = new LoadSettings { BlockLocalFileAccess = true, LoadErrorHandling = ContentErrorHandling.Ignore }));
        yield return ("21-table-over-pages", Doc("<table border=\"1\" cellspacing=\"0\" style=\"width:100%\"><thead><tr><th>No</th><th>Item</th><th>Amount</th></tr></thead><tbody>"
            + string.Concat(System.Linq.Enumerable.Range(1, 80).Select(i => $"<tr><td>{i}</td><td>Item {i}</td><td style=\"text-align:right\">{i * 12.5:0.00}</td></tr>")) + "</tbody></table>"));
        yield return ("22-page-offset-footer", Doc(Long(4), g => g.PageOffset = 10, o => { o.PagesCount = true; o.FooterSettings = new FooterSettings { Right = "Page [page]" }; }));
        yield return ("23-european-text", Doc("<h1>Grüße aus Köln</h1><p>Ça coûte 12,50 € — naïve café, smørrebrød, Łódź, Ελληνικά, Русский.</p>"));
        yield return ("24-dpi-image-quality", Doc($"<img src=\"{uri("red.png")}\" style=\"width:300px;height:300px\">", g => { g.DPI = 300; g.ImageDPI = 150; g.ImageQuality = 80; }));
        yield return ("25-smart-shrinking-off", Doc("<div style=\"width:1400px;border:1px solid #000\">A box wider than the page</div>" + Text, null, o => o.WebSettings = new WebSettings { EnableIntelligentShrinking = false }));
        yield return ("26-viewport", Doc("<p>Viewport</p>" + Text, g => g.ViewportSize = "1280x1024"));
        yield return ("27-no-compression", Doc("<h1>Uncompressed</h1>" + Text, g => g.UseCompression = false));
    }
}
