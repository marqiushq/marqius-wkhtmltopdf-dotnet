# Make PDFs in .NET: the Marqius.DinkToPdf manual

This manual shows you, step by step, how to turn HTML into PDF in a .NET app. Each chapter has one goal, the files to copy, the commands to run and what you will see. You need no experience with PDFs.

Every example in this manual is run exactly as printed, on Windows, Linux and macOS, each time the package changes.

**Contents**

1. [Set up](#1-set-up)
2. [Your first PDF](#2-your-first-pdf)
3. [An HTML template, filled with your data](#3-an-html-template-filled-with-your-data)
4. [Paper, margins, headers and footers](#4-paper-margins-headers-and-footers)
5. [A PDF of a web page](#5-a-pdf-of-a-web-page)
6. [Several parts in one PDF](#6-several-parts-in-one-pdf)
7. [PDFs from a website (ASP.NET Core)](#7-pdfs-from-a-website-aspnet-core)
8. [Other languages and fonts](#8-other-languages-and-fonts)
9. [Run it in Docker](#9-run-it-in-docker)
10. [When a PDF does not come out right](#10-when-a-pdf-does-not-come-out-right)
11. [Already using DinkToPdf?](#11-already-using-dinktopdf)
12. [Run the program yourself, or with Rotativa](#12-run-the-program-yourself-or-with-rotativa)
13. [Licence](#13-licence)
14. [Questions](#14-questions)

## 1. Set up

**Step 1.** Install the .NET SDK, version 8 or later, from https://dotnet.microsoft.com/download.

**Step 2.** Open a terminal (on Windows: PowerShell or Command Prompt) and check that it works:

```bash
dotnet --version
```

You see a version number, such as `8.0.416`. Any editor works: Visual Studio, Visual Studio Code or Rider.

## 2. Your first PDF

**Step 1.** Make a new app, go into its folder, and add the package:

```bash
dotnet new console -o Invoice
cd Invoice
dotnet add package Marqius.DinkToPdf
```

**Step 2.** Open `Program.cs`, delete everything in it, and paste this:

<!-- example: manual first Program.cs -->
```csharp
using DinkToPdf;

var converter = new SynchronizedConverter(new PdfTools());

var document = new HtmlToPdfDocument
{
    GlobalSettings = { PaperSize = PaperKind.A4 },
    Objects = { new ObjectSettings { HtmlContent = "<h1>Invoice 42</h1><p>Thank you for your order.</p>" } },
};

byte[] pdf = converter.Convert(document);
File.WriteAllBytes("invoice.pdf", pdf);
Console.WriteLine("invoice.pdf is ready");
```

**Step 3.** Run it:

```bash
dotnet run
```

You see `invoice.pdf is ready`. Open `invoice.pdf` in the `Invoice` folder.

**What each part does:**
- `SynchronizedConverter` makes the PDFs. Make one, and use it for every PDF.
- `HtmlToPdfDocument` is what to make: `GlobalSettings` for the whole PDF, and `Objects` for its pages.
- `HtmlContent` is your HTML, as text.
- `Convert` gives back the PDF as bytes, ready to save, send or store.

## 3. An HTML template, filled with your data

Real documents have a design: a template with its own style sheet and logo. Here you keep them in a `templates` folder and fill them with your data.

**Step 1.** Make a new app and add the package:

```bash
dotnet new console -o InvoiceApp
cd InvoiceApp
dotnet add package Marqius.DinkToPdf
mkdir templates
```

**Step 2.** In the `templates` folder, make three files.

`invoice.html`, the page, with `{{...}}` where your data goes:

<!-- example: manual template invoice.html -->
```html
<!DOCTYPE html>
<html>
<head>
  <meta charset="utf-8">
  <base href="{{folder}}">
  <link rel="stylesheet" href="style.css">
</head>
<body>
  <img src="logo.svg" class="logo">
  <h1>Invoice {{number}}</h1>
  <p>Bill to: {{customer}}</p>
  <table>
    <tr><th>Item</th><th>Amount</th></tr>
    {{rows}}
  </table>
  <p class="total">Total: {{total}}</p>
</body>
</html>
```

`style.css`, its design:

<!-- example: manual template style.css -->
```css
body { font-family: Arial, sans-serif; color: #222; }
.logo { width: 120px; }
h1 { color: #1a5fb4; }
table { width: 100%; border-collapse: collapse; }
th, td { border-bottom: 1px solid #ccc; padding: 6px; text-align: left; }
.total { font-size: 18px; font-weight: bold; }
```

`logo.svg`, a logo (use your own: PNG, JPG and SVG all work):

<!-- example: manual template logo.svg -->
```xml
<svg xmlns="http://www.w3.org/2000/svg" width="240" height="60" viewBox="0 0 240 60">
  <rect width="240" height="60" rx="8" fill="#1a5fb4"/>
  <text x="120" y="38" font-family="Arial" font-size="24" fill="white" text-anchor="middle">Northwind</text>
</svg>
```

**Step 3.** Open `InvoiceApp.csproj` and paste these lines just above the last line, `</Project>`. They copy the templates next to your app each time it is built:

<!-- example: manual template csproj -->
```xml
  <ItemGroup>
    <None Update="templates\**" CopyToOutputDirectory="PreserveNewest" />
  </ItemGroup>
```

**Step 4.** Open `Program.cs`, delete everything in it, and paste this:

<!-- example: manual template Program.cs -->
```csharp
using System.Net;
using DinkToPdf;

// Where the templates are: copied next to the app when it is built
string folder = Path.Combine(AppContext.BaseDirectory, "templates");

// Your data (in a real app, from your database)
string number = "42";
string customer = "Northwind Traders";
var lines = new (string Item, decimal Amount)[]
{
    ("Paper, A4, 500 sheets", 4.99m),
    ("Ink cartridge", 19.50m),
};

// One table row for each line of the invoice
string rows = "";
foreach (var line in lines)
{
    rows += $"<tr><td>{WebUtility.HtmlEncode(line.Item)}</td><td>{line.Amount:0.00}</td></tr>";
}

// The template, filled in
string html = File.ReadAllText(Path.Combine(folder, "invoice.html"))
    .Replace("{{folder}}", new Uri(folder + Path.DirectorySeparatorChar).AbsoluteUri)
    .Replace("{{number}}", WebUtility.HtmlEncode(number))
    .Replace("{{customer}}", WebUtility.HtmlEncode(customer))
    .Replace("{{rows}}", rows)
    .Replace("{{total}}", lines.Sum(line => line.Amount).ToString("0.00"));

var converter = new SynchronizedConverter(new PdfTools());
var document = new HtmlToPdfDocument
{
    GlobalSettings = { PaperSize = PaperKind.A4 },
    Objects = { new ObjectSettings { HtmlContent = html } },
};

File.WriteAllBytes("invoice.pdf", converter.Convert(document));
Console.WriteLine("invoice.pdf is ready");
```

**Step 5.** Run it:

```bash
dotnet run
```

Open `invoice.pdf`: the logo, the blue heading and the table are there.

**Why it is done this way:**
- `WebUtility.HtmlEncode` makes your data safe in HTML. A customer named `Ann & <Co>` prints as written, and cannot break the page.
- `<base href="{{folder}}">` tells the page where its files are, so `style.css` and `logo.svg` load. Without it, HTML given as text cannot find files next to it.
- The `.csproj` lines copy the templates next to your app, so the same code works after `dotnet publish`, in Docker and on IIS.

## 4. Paper, margins, headers and footers

Paste this into `Program.cs` of any app from chapter 2 or 3. Each line says what it does.

<!-- example: manual settings Program.cs -->
```csharp
using DinkToPdf;

var converter = new SynchronizedConverter(new PdfTools());

var document = new HtmlToPdfDocument
{
    GlobalSettings =
    {
        PaperSize = PaperKind.A4,                    // A4, Letter, Legal, A5 ...
        Orientation = Orientation.Landscape,         // or Orientation.Portrait
        Margins = new MarginSettings(10, 10, 10, 10), // top, right, bottom, left, in millimetres
        DocumentTitle = "Invoice 42",                // the title PDF readers show
    },
    Objects =
    {
        new ObjectSettings
        {
            HtmlContent = "<h1>Invoice 42</h1><p>Thank you for your order.</p>",
            WebSettings = { DefaultEncoding = "utf-8", PrintMediaType = true }, // use the page's print styles
            HeaderSettings = { Right = "Page [page] of [toPage]", FontSize = 9, Line = true },
            FooterSettings = { Center = "Northwind Supplies Ltd", FontSize = 8 },
        },
    },
};

File.WriteAllBytes("invoice.pdf", converter.Convert(document));
Console.WriteLine("invoice.pdf is ready");
```

Run it with `dotnet run`, and open `invoice.pdf`: a landscape page, with "Page 1 of 1" at the top right.

**The settings you will use most:**

| You want | Write |
|---|---|
| A paper size | `PaperSize = PaperKind.Letter` (or `A4`, `Legal`, `A5` ...) |
| A paper size of your own | `PaperSize = new PechkinPaperSize("100mm", "150mm")` |
| Landscape | `Orientation = Orientation.Landscape` |
| Margins | `Margins = new MarginSettings(10, 10, 10, 10)`: top, right, bottom, left, in millimetres |
| Page numbers | `FooterSettings = { Center = "Page [page] of [toPage]" }` |
| A header or footer line | `HeaderSettings = { Line = true }` |
| The page's print styles (`@media print`) | `WebSettings = { PrintMediaType = true }` |

In headers and footers, `[page]` becomes the page number and `[toPage]` the number of pages.

## 5. A PDF of a web page

Give a web address in `Page` in place of `HtmlContent`:

<!-- example: manual web Program.cs -->
```csharp
using DinkToPdf;

var converter = new SynchronizedConverter(new PdfTools());

var document = new HtmlToPdfDocument
{
    GlobalSettings = { PaperSize = PaperKind.A4 },
    Objects = { new ObjectSettings { Page = "https://example.com" } },
};

File.WriteAllBytes("page.pdf", converter.Convert(document));
Console.WriteLine("page.pdf is ready");
```

`Page` also takes a file on your computer, such as `Page = "C:\\invoices\\42.html"` or `Page = "/srv/invoices/42.html"`. A page behind a sign-in needs its cookie or header, for example `LoadSettings = { Cookies = new Dictionary<string, string> { ["session"] = "..." } }`.

## 6. Several parts in one PDF

Each object in `Objects` starts on a new page, and all of them make one PDF:

<!-- example: manual parts Program.cs -->
```csharp
using DinkToPdf;

var converter = new SynchronizedConverter(new PdfTools());

var document = new HtmlToPdfDocument
{
    GlobalSettings = { PaperSize = PaperKind.A4, DocumentTitle = "Annual report" },
    Objects =
    {
        // the cover
        new ObjectSettings { HtmlContent = "<h1 style='margin-top:300px; text-align:center'>Annual report 2026</h1>" },
        // the chapters, numbered at the foot of each page
        new ObjectSettings { HtmlContent = "<h2>1. Sales</h2><p>Sales grew in every region.</p>", FooterSettings = { Center = "Page [page] of [toPage]" } },
        new ObjectSettings { HtmlContent = "<h2>2. Outlook</h2><p>We expect steady growth.</p>", FooterSettings = { Center = "Page [page] of [toPage]" } },
    },
};

File.WriteAllBytes("report.pdf", converter.Convert(document));
Console.WriteLine("report.pdf is ready");
```

`report.pdf` has three pages: the cover, then "Page 2 of 3" and "Page 3 of 3".

## 7. PDFs from a website (ASP.NET Core)

### A minimal API

**Step 1.** Make a new website and add the package:

```bash
dotnet new web -o InvoiceSite
cd InvoiceSite
dotnet add package Marqius.DinkToPdf
```

**Step 2.** Open `Program.cs`, delete everything in it, and paste this:

<!-- example: manual aspnet Program.cs -->
```csharp
using DinkToPdf;
using DinkToPdf.Contracts;
using Marqius.DinkToPdf; // ConvertAsync

var builder = WebApplication.CreateBuilder(args);

// One converter for the whole website
builder.Services.AddSingleton<IConverter>(new SynchronizedConverter(new PdfTools()));

var app = builder.Build();

app.MapGet("/invoice", async (IConverter converter, CancellationToken stop) =>
{
    var document = new HtmlToPdfDocument
    {
        GlobalSettings = { PaperSize = PaperKind.A4 },
        Objects = { new ObjectSettings { HtmlContent = "<h1>Invoice 42</h1><p>Thank you for your order.</p>" } },
    };
    byte[] pdf = await converter.ConvertAsync(document, stop);
    return Results.File(pdf, "application/pdf", "invoice.pdf");
});

app.Run();
```

**Step 3.** Run it with `dotnet run`. It prints an address such as `http://localhost:5000`. Open that address with `/invoice` at the end, and the PDF downloads.

**Why it is done this way:**
- One converter for the whole website, as a singleton: it makes many PDFs at once, side by side.
- `ConvertAsync` makes the PDF without holding a thread, and `stop` stops it if the visitor leaves.

### In an MVC controller

**Step 1.** Make an MVC website and add the package:

```bash
dotnet new mvc -o Shop
cd Shop
dotnet add package Marqius.DinkToPdf
```

**Step 2.** Open `Program.cs`. Under the line `builder.Services.AddControllersWithViews();`, add these two lines:

<!-- example: manual mvc Program.cs -->
```csharp
// One converter for the whole website
builder.Services.AddSingleton<DinkToPdf.Contracts.IConverter>(new DinkToPdf.SynchronizedConverter(new DinkToPdf.PdfTools()));
```

**Step 3.** In the `Controllers` folder, make a file named `InvoiceController.cs`, and paste this:

<!-- example: manual mvc InvoiceController.cs -->
```csharp
using DinkToPdf;
using DinkToPdf.Contracts;
using Marqius.DinkToPdf;
using Microsoft.AspNetCore.Mvc;

namespace Shop.Controllers;

public class InvoiceController : Controller
{
    private readonly IConverter converter;

    public InvoiceController(IConverter converter)
    {
        this.converter = converter;
    }

    // GET /Invoice/Download/42
    public async Task<IActionResult> Download(int id, CancellationToken stop)
    {
        var document = new HtmlToPdfDocument
        {
            GlobalSettings = { PaperSize = PaperKind.A4 },
            Objects = { new ObjectSettings { HtmlContent = $"<h1>Invoice {id}</h1><p>Thank you for your order.</p>" } },
        };
        byte[] pdf = await converter.ConvertAsync(document, stop);
        return File(pdf, "application/pdf", $"invoice-{id}.pdf");
    }
}
```

**Step 4.** Run it with `dotnet run`, and open the address it prints with `/Invoice/Download/42` at the end. `invoice-42.pdf` downloads.

## 8. Other languages and fonts

marqius-wkhtmltopdf prints English, Arabic, Hebrew, Hindi, Thai, Chinese, Japanese, Korean and more, as real text that can be searched and copied. Letters are drawn with the fonts installed on the computer.

<!-- example: manual languages Program.cs -->
```csharp
using DinkToPdf;

var converter = new SynchronizedConverter(new PdfTools());

// Tells you when a font is missing, and which free font has those letters
converter.Warning += (sender, e) => Console.WriteLine("Warning: " + e.Message);

var document = new HtmlToPdfDocument
{
    Objects =
    {
        new ObjectSettings
        {
            HtmlContent = """
                <meta charset="utf-8">
                <h1>Thank you</h1>
                <p>Arabic: شكرا لك</p>
                <p>Hebrew: תודה</p>
                <p>Hindi: धन्यवाद</p>
                <p>Thai: ขอบคุณ</p>
                <p>Chinese: 谢谢</p>
                <p>Japanese: ありがとう</p>
                <p>Korean: 감사합니다</p>
                """,
        },
    },
};

File.WriteAllBytes("thanks.pdf", converter.Convert(document));
Console.WriteLine("thanks.pdf is ready");
```

Windows and macOS have fonts for all of these. A Linux server or a Docker image may not: then the `Warning` event names the font that is missing, and the PDF is still made. To install the fonts for all of these languages on Debian or Ubuntu:

```bash
sudo apt-get install -y fonts-noto-core fonts-noto-cjk
```

`fonts-noto-core` has Arabic, Hebrew, Hindi and Thai, and `fonts-noto-cjk` has Chinese, Japanese and Korean.

## 9. Run it in Docker

**Step 1.** Make the website from chapter 7 ("A minimal API").

**Step 2.** In its folder, make a file named `Dockerfile` with this inside:

<!-- example: manual docker Dockerfile -->
```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:8.0
RUN apt-get update && apt-get install -y --no-install-recommends fonts-liberation && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY publish/ .
ENTRYPOINT ["dotnet", "InvoiceSite.dll"]
```

**Step 3.** Build the website, then the image, then run it:

```bash
dotnet publish -c Release -o publish
docker build -t invoice-site .
docker run --rm -p 8080:8080 invoice-site
```

**Step 4.** Open http://localhost:8080/invoice and the PDF downloads.

The `RUN` line installs fonts: .NET's images have none, and a PDF needs at least one. For other languages, install their fonts on the same line:

<!-- example: manual docker languages -->
```dockerfile
RUN apt-get update && apt-get install -y --no-install-recommends fonts-liberation fonts-noto-core fonts-noto-cjk && rm -rf /var/lib/apt/lists/*
```

On Alpine (`aspnet:8.0-alpine`), use `RUN apk add --no-cache font-liberation`. Use the image tag of your .NET version (`8.0`, `9.0`, `10.0`).

## 10. When a PDF does not come out right

Listen to two events. They say what went wrong, and what is worth knowing:

<!-- example: manual errors Program.cs -->
```csharp
using DinkToPdf;

var converter = new SynchronizedConverter(new PdfTools());

// What stopped a PDF, and what is worth knowing
converter.Error += (sender, e) => Console.WriteLine("Error: " + e.Message);
converter.Warning += (sender, e) => Console.WriteLine("Warning: " + e.Message);

// Stop any PDF that takes more than 30 seconds
PdfTools.Timeout = TimeSpan.FromSeconds(30);

var document = new HtmlToPdfDocument
{
    Objects = { new ObjectSettings { HtmlContent = "<h1>Report</h1><img src=\"chart.png\">" } },
};

byte[] pdf = converter.Convert(document);
if (pdf.Length > 0)
{
    File.WriteAllBytes("report.pdf", pdf);
    Console.WriteLine("report.pdf is ready");
}
else
{
    Console.WriteLine("No PDF was made: the error above says why");
}
```

Run it: the warning says that `chart.png` could not be loaded, and the PDF is made without it.

| You see | Do this |
|---|---|
| An empty PDF (`Convert` gives no bytes) | Read the `Error` message. Most often the page or file in `Page` cannot be reached. |
| Pictures or styles missing | Give full addresses (`https://...`), or a `<base href>` as in chapter 3. |
| Boxes in place of letters | Install a font that has those letters (chapter 8). |
| A PDF that never ends | Set `PdfTools.Timeout`, as above. |
| `Unable to load shared library 'libwkhtmltox.so'` | You are on the original DinkToPdf, which loads a native file. Marqius.DinkToPdf loads none — see below. |
| `System.BadImageFormatException (0x8007000B)` | The original DinkToPdf again: the native file does not match the CPU. Cannot happen here — see below. |

### The two errors you will never see here

Both come from the original DinkToPdf, which loads a native file called
`libwkhtmltox` — one build per operating system and per CPU, installed by hand
beside your app. When that file is missing, or built for a different machine,
this is what you see, word for word:

```text
Unhandled exception. System.DllNotFoundException: Unable to load shared
library '/app/libwkhtmltox.so' or one of its dependencies.
```

```text
System.BadImageFormatException: An attempt was made to load a program with an
incorrect format. (Exception from HRESULT: 0x8007000B)
   at DinkToPdf.WkHtmlToXBindings.wkhtmltopdf_init(Int32 useGraphics)
```

Marqius.DinkToPdf does not load a native library, because there is none. What
runs is the marqius-wkhtmltopdf program — one statically linked file, brought
in with the package for your platform, selected by your runtime identifier.
There is no file to be missing, and no wrong build to mismatch.

If your project still shows either error, chapter 11 switches it in three
steps. The full story of the first error — why it appears in Docker and on
ARM, and the manual fix if you stay on the original — is here:
[Unable to load shared library 'libwkhtmltox.so': the cause and the fix](https://marqius.com/blog/dinktopdf-unable-to-load-libwkhtmltox/)

## 11. Already using DinkToPdf?

Your code stays as it is.

**Step 1.** In your project's folder, run:

```bash
dotnet remove package DinkToPdf
dotnet add package Marqius.DinkToPdf
```

**Step 2.** Delete the `libwkhtmltox.dll`, `libwkhtmltox.so` or `libwkhtmltox.dylib` files from your project. If your app loads that file in code (a `CustomAssemblyLoadContext`), delete those lines too.

**Step 3.** Run your app as usual. It makes the same PDFs, with no native library to load, side by side for many users at once.

## 12. Run the program yourself, or with Rotativa

`Marqius.DinkToPdf` uses the program marqius-wkhtmltopdf, from the package `Marqius.Wkhtmltopdf`. To run that program yourself, with wkhtmltopdf's command line, add that package:

```bash
dotnet new console -o Statement
cd Statement
dotnet add package Marqius.Wkhtmltopdf
```

Then paste this into `Program.cs`:

<!-- example: manual program Program.cs -->
```csharp
using System.Diagnostics;
using Marqius.Wkhtmltopdf;

// The HTML to turn into a PDF
File.WriteAllText("statement.html", "<h1>Statement for March</h1><p>Balance: 120.00</p>");

// Run the program, as you would run wkhtmltopdf: options, input file, output file
var run = Process.Start(Wkhtmltopdf.StartInfo(new[] { "--page-size", "A4", "statement.html", "statement.pdf" }))!;
string messages = run.StandardError.ReadToEnd(); // read before waiting, so a long run cannot fill the pipe
run.WaitForExit();

Console.WriteLine(run.ExitCode == 0 ? "statement.pdf is ready" : messages);
```

Run it with `dotnet run`. Every wkhtmltopdf option works in that list. `Wkhtmltopdf.Path` is the program's full path, for any library that asks where wkhtmltopdf is. For Rotativa, see [the two lines that set it up](https://github.com/marqiushq/marqius-wkhtmltopdf-dotnet/blob/main/src/Marqius.Wkhtmltopdf/README.md#use-it-with-rotativa-aspnet-core-mvc).

## 13. Licence

### Free for personal, teaching and open-source projects

The Community licence is free for personal projects, for teaching and learning, and for open-source projects, including their CI runs and documentation builds.

**Step 1.** Open [marqius.com/licence/marqius-wkhtmltopdf](https://marqius.com/licence/marqius-wkhtmltopdf), click **Get your free key**, and sign in with Google.

**Step 2.** Tick "I'll use marqius-wkhtmltopdf only for personal, teaching or open-source work", click **Get my free key**, then **Copy key**.

**Step 3.** Give the key to your app, in one of two ways:

- **In the environment** of the computer, container or CI job that makes the PDFs, as `MARQIUS_LICENCE`. For example, `docker run -e MARQIUS_LICENCE="your key" ...`, or in PowerShell `setx MARQIUS_LICENCE "your key"` and then a new terminal.
- **In your app's settings**, such as `appsettings.json` or user secrets, under `Marqius:Licence`. Then add one line to `Program.cs`, before the first PDF:

<!-- example: manual licence Program.cs -->
```csharp
Environment.SetEnvironmentVariable("MARQIUS_LICENCE", builder.Configuration["Marqius:Licence"]);
```

The key is valid for a year. When it has less than 30 days left, sign in again and click **Get a new key**.

### Paid for businesses

Anything done by or for a business, including internal tools and client work, needs a paid licence. [See the plans and prices](https://marqius.com/licence/marqius-wkhtmltopdf). Give the licence to your app in the same way.

The licence is checked on your own computer, and is never sent anywhere.

## 14. Questions

**Do I need to install wkhtmltopdf or libwkhtmltox?** No. The package has everything, for Windows x64, Linux x64 and ARM64 (Alpine too) and macOS on Apple silicon.

**Which .NET versions does it work with?** .NET 8, 9 and 10, and .NET Framework 4.6.2 or later.

**Can many users make PDFs at the same time?** Yes. Each PDF is made by its own process, as many at once as the server has processors.

**Can I publish my app for one system, or as one file?** Yes. `dotnet publish -c Release -r linux-x64` copies only that system's program. Self-contained, single-file, trimmed and Native AOT apps all work.

**Does it work on IIS?** Yes. If the app pool cannot write its usual cache folder, the program uses the temporary folder.

**Where is each setting explained?** In your editor: every class, setting and parameter has its help, which shows as you type.

**Where do I get help?** Write to hello@marqius.com, or [open an issue](https://github.com/marqiushq/marqius-wkhtmltopdf-dotnet/issues).
