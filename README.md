# DinkToPdf for .NET 8, without libwkhtmltox: Marqius.DinkToPdf

[![NuGet](https://img.shields.io/nuget/v/Marqius.DinkToPdf)](https://www.nuget.org/packages/Marqius.DinkToPdf)

**Marqius.DinkToPdf turns HTML into PDF in .NET, with the same code as DinkToPdf.** It has DinkToPdf's classes and settings, but it needs no `libwkhtmltox` file. Instead it runs marqius-wkhtmltopdf, the maintained replacement for wkhtmltopdf, which comes inside the package.

- **No more "Unable to load shared library 'libwkhtmltox'".** There is no native library to load, so that error, `DllNotFoundException` and `BadImageFormatException` cannot happen.
- **Your code stays the same.** Every class, method, event and setting of DinkToPdf 1.0.8, with the same names, in the same `DinkToPdf` namespace. Checked on every build.
- **Fast, and safe with many users at once.** On the same machine, each PDF takes 7 ms, where DinkToPdf takes 212 ms. Each PDF is made in its own process, so requests run side by side. See [How fast is it?](#how-fast-is-it)
- **Works everywhere .NET runs:** Windows, Linux (also Alpine and Docker), macOS; .NET 8, 9 and 10, and .NET Framework 4.6.2 or later.
- **Many languages.** English, Arabic, Hebrew, Hindi, Thai, Chinese, Japanese, Korean and more, as real text you can search and copy.

**New to .NET or to PDFs?** [The manual](https://github.com/marqiushq/marqius-wkhtmltopdf-dotnet/blob/main/docs/manual.md) takes you step by step: templates with your data, headers and footers, websites, Docker, other languages, and what to do when a PDF does not come out right.

## Already using DinkToPdf? Switch in 3 steps

**Step 1.** In your project's folder, run these two commands:

```bash
dotnet remove package DinkToPdf
dotnet add package Marqius.DinkToPdf
```

**Step 2.** Delete the `libwkhtmltox.dll`, `libwkhtmltox.so` or `libwkhtmltox.dylib` files from your project. If your app loads that file in code, delete those lines too. They look like this:

```csharp
var context = new CustomAssemblyLoadContext();
context.LoadUnmanagedLibrary(Path.Combine(Directory.GetCurrentDirectory(), "libwkhtmltox.dll"));
```

**Step 3.** Run your app as usual. It makes the same PDFs. Nothing else in your code changes.

### Will my existing code keep working?

Yes. Each point below is tested on every build, on Windows, Linux and macOS:

- **Same API.** All of DinkToPdf 1.0.8's API is here, unchanged: every class, method, property, event, setting and setting name, compared line by line with DinkToPdf 1.0.8 from nuget.org. That includes `WkHtmlToXBindings`, DinkToPdf's raw calls, which now work without libwkhtmltox.
- **Nothing new in `DinkToPdf`.** The `DinkToPdf` namespace has nothing that DinkToPdf 1.0.8 did not have, so nothing can clash with your code. What this package adds is in the namespace `Marqius.DinkToPdf`, which you add only when you want it.
- **Code already built against DinkToPdf runs too.** Another package that uses DinkToPdf, for example, runs unchanged. If a package brings DinkToPdf into your app, the build uses Marqius.DinkToPdf in its place.
- **Same behaviour.** `Convert` returns the PDF, or an empty array when it can't be made, as before. Every event comes before `Convert` returns, and handlers are called one at a time. Relative paths such as `Out = "report.pdf"` mean your app's folder.

## New here? Make your first PDF in 5 minutes

**Step 1.** Install the .NET 8 SDK from https://dotnet.microsoft.com/download, then check it works:

```bash
dotnet --version
```

**Step 2.** Make a new app and go into its folder:

```bash
dotnet new console -o Invoice
cd Invoice
```

**Step 3.** Add the package:

```bash
dotnet add package Marqius.DinkToPdf
```

**Step 4.** Open `Program.cs`, delete everything in it, and paste this:

<!-- example: quickstart Program.cs -->
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

**Step 5.** Run it:

```bash
dotnet run
```

You see `invoice.pdf is ready`, and `invoice.pdf` is in the `Invoice` folder. Open it.

## A PDF from a website (ASP.NET Core)

**Step 1.** Make a new website and add the package:

```bash
dotnet new web -o InvoiceSite
cd InvoiceSite
dotnet add package Marqius.DinkToPdf
```

**Step 2.** Open `Program.cs`, delete everything in it, and paste this:

<!-- example: aspnet Program.cs -->
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

**Step 3.** Run it:

```bash
dotnet run
```

It prints an address such as `http://localhost:5000`. Open that address with `/invoice` at the end, and the PDF downloads.

`ConvertAsync` makes the PDF without blocking a thread. If the visitor closes the page, `stop` stops the PDF too. It comes with `using Marqius.DinkToPdf;`, not with `DinkToPdf`, so it never clashes with a `ConvertAsync` your code already has.

## Common settings

Copy this into `Program.cs` to see the settings people use most. Each line says what it does.

<!-- example: settings Program.cs -->
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

To make a PDF of a web page instead of your own HTML, use `Page` in place of `HtmlContent`:

```csharp
new ObjectSettings { Page = "https://example.com" }
```

## In Docker

**Step 1.** Make the website from the section above.

**Step 2.** In its folder, make a file named `Dockerfile` with this inside:

<!-- example: docker Dockerfile -->
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

The `RUN` line installs fonts: the .NET images have none, and a PDF needs at least one. On Alpine (`aspnet:8.0-alpine`), use `RUN apk add --no-cache font-liberation` instead. Use the image tag of your .NET version (`8.0`, `9.0`, `10.0`).

## Fonts and other languages

Letters are drawn with the fonts installed on the computer. If a page has letters that no installed font has (Japanese, for example), the PDF is still made, and the `Warning` event tells you which free font to get and gives you the command:

```
no installed font has those letters. Download the open font that has them, once, and make the PDF again:
    marqius-wkhtmltopdf fonts get "Noto Sans JP"
```

To see these messages, listen to the events:

```csharp
converter.Warning += (sender, e) => Console.WriteLine("Warning: " + e.Message);
converter.Error += (sender, e) => Console.WriteLine("Error: " + e.Message);
```

If you don't listen to them, they are not printed, as in DinkToPdf. One exception: when a PDF can't be made and nothing listens to `Error`, the reason is written to the console's error output, so it shows up in your server's log.

## Licence and trial

marqius-wkhtmltopdf is commercial software with a 30-day free trial.
- **After the trial,** without a licence, each page gets one small line: "Prepared by Marqius, Purchase Licence". Your PDFs never stop.
- **Personal, teaching and open-source use** is free with a Community licence.
- **To use a licence,** set the environment variable `MARQIUS_LICENCE` to the licence, or to the path of a file that holds it.

[See the plans and prices](https://marqius.com/licence/marqius-wkhtmltopdf).

## Questions

**Why did DinkToPdf say "Unable to load shared library 'libwkhtmltox'"?** DinkToPdf needs the file `libwkhtmltox` next to your app, in the right 32-bit or 64-bit version, with its system libraries installed. Marqius.DinkToPdf does not use that file, so this error cannot happen.

**Why did DinkToPdf say "BadImageFormatException ... (0x8007000B)"?** A 32-bit `libwkhtmltox.dll` was loaded into a 64-bit app, or the other way round. Marqius.DinkToPdf has no DLL to mix up.

**Can many users make PDFs at the same time?** Yes. Each PDF runs in its own process, as many at once as the server has processors.

**What about IIS?** Each PDF runs in its own process, so an app pool recycle cannot break the converter. If the app pool's account cannot write its usual cache folder, the package uses the temporary folder, so PDFs stay fast.

**How do I stop a PDF that takes too long?** Set a time limit once, when the app starts: `PdfTools.Timeout = TimeSpan.FromSeconds(30);`

**How do I publish for one system only?** Add `-r`, for example `dotnet publish -c Release -r linux-x64`. Only that system's program is copied. Self-contained, single-file, trimmed and Native AOT apps all work.

## How fast is it?

The same invoice ([`tests/bench/invoice.html`](https://github.com/marqiushq/marqius-wkhtmltopdf-dotnet/blob/main/tests/bench/invoice.html)) was made by three converters, one after another, on the same GitHub machine (Linux, 4 cores). [See the run on GitHub.](https://github.com/marqiushq/marqius-wkhtmltopdf-dotnet/actions/runs/45357818586)

| | Marqius.DinkToPdf | DinkToPdf 1.0.8 (libwkhtmltox 0.12.4) | PuppeteerSharp 25.18.1 (Chrome 157) |
|---|---|---|---|
| First PDF after the app starts | **65 ms** | 245 ms | 504 ms |
| Each PDF after that (median of 30) | **7.1 ms** | 212 ms | 64 ms |
| 100 PDFs, 8 at a time | **265 a second** | 4.7 a second | 28 a second |
| Space it needs on the server | **23 MB** | 45 MB | 725 MB |

Each converter is used the way its own examples show. DinkToPdf runs with `SynchronizedConverter`, and PuppeteerSharp keeps one browser open with a new page for each PDF. "Space it needs" means the published app plus what it loads: libwkhtmltox for DinkToPdf, and the Chromium download for PuppeteerSharp. To measure it yourself, open this repository's Actions tab, choose Benchmark, and click Run workflow.

## What is different from DinkToPdf

| | DinkToPdf | Marqius.DinkToPdf |
|---|---|---|
| What it loads | `libwkhtmltox` 0.12.4 (2016), inside your app | nothing: the program runs next to your app |
| Many PDFs at once | one at a time | side by side, as many as the server has processors |
| `await` | no | `ConvertAsync` (with `using Marqius.DinkToPdf;`), with cancelling |
| Files on the server (`file:///`) | load | load |
| Text size on Windows | about an eighth bigger than on Linux | the same as on Linux, on every system |
| A table header repeated on a new page | drawn over that page's first row | drawn above it |
| A PDF that can't be made, with no `Error` handler | says nothing | writes the reason to the console's error output |
| `Tools.GetLibraryVersion()` | `0.12.4` | `0.12.6` |
| Still updated | last release 2017 | yes |

## How it is tested

Every change is tested on GitHub before it is published:
- Windows Server 2025 and 2022, Linux x64 and ARM64, and macOS.
- Each example in this page, and in [the manual](https://github.com/marqiushq/marqius-wkhtmltopdf-dotnet/blob/main/docs/manual.md), is run exactly as printed.
- DinkToPdf's own example app runs unchanged.
- DinkToPdf 1.0.8's whole API is compared, line by line, with this package's.
- A library built against DinkToPdf 1.0.8 runs on this package, and the same code compiles against it with every warning treated as an error.
- Docker images are built on Debian and Alpine.
- Publishing is tried self-contained, single-file, trimmed and Native AOT.

27 documents, covering every DinkToPdf setting that changes a PDF, are made by both DinkToPdf and Marqius.DinkToPdf, and compared page by page. See the "Test" and "Beside DinkToPdf" runs in this repository's [Actions](https://github.com/marqiushq/marqius-wkhtmltopdf-dotnet/actions).

Marqius.DinkToPdf's own code is MIT, and is built on [DinkToPdf](https://github.com/rdvojmoc/DinkToPdf) by Rok Dvojmoc (MIT); see [LICENSE.md](https://github.com/marqiushq/marqius-wkhtmltopdf-dotnet/blob/main/LICENSE.md). marqius is not connected to DinkToPdf or its author. For Rotativa, NReco.PdfGenerator or your own code, use the program alone: [Marqius.Wkhtmltopdf](https://www.nuget.org/packages/Marqius.Wkhtmltopdf).
