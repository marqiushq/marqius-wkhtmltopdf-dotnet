# DinkToPdf without libwkhtmltox: Marqius.DinkToPdf for .NET 8, .NET Framework and Docker

[![NuGet](https://img.shields.io/nuget/v/Marqius.DinkToPdf)](https://www.nuget.org/packages/Marqius.DinkToPdf)

**Marqius.DinkToPdf is DinkToPdf with marqius-wkhtmltopdf in place of libwkhtmltox.** Replace one package, delete libwkhtmltox, and your code makes PDFs as before: the same `DinkToPdf` namespace, `SynchronizedConverter`, `PdfTools`, `HtmlToPdfDocument` and every setting. marqius-wkhtmltopdf is the maintained drop-in replacement for wkhtmltopdf, whose project was archived in January 2023.

- **No native library to load.** No `Unable to load shared library 'libwkhtmltox'`, no `DllNotFoundException`, no `BadImageFormatException` (0x8007000B), no 32-bit or 64-bit DLL to pick.
- **Your code stays as it is.** DinkToPdf's own classes, settings and events, compiled from DinkToPdf's code.
- **Safe across threads.** Each PDF is made by a process of its own, so requests convert side by side instead of waiting for one worker thread, and an IIS recycle cannot crash the converter.
- **Windows, Linux and macOS.** Windows x64, Linux x64 and ARM64 (Alpine too), macOS on Apple silicon; .NET 8, 9 and 10, and .NET Framework 4.6.2 and later.
- **Many languages, even on one page.** Arabic and Hebrew from right to left, Hindi, Thai, Chinese, Japanese and Korean as well as English, all as text you can search and copy.

## Switch from DinkToPdf in three steps

**1. Replace the package** in your project:

```bash
dotnet remove package DinkToPdf
dotnet add package Marqius.DinkToPdf
```

**2. Delete libwkhtmltox** (`libwkhtmltox.dll`, `.so` or `.dylib`) from your project, and the line that loads it, if your app has one:

```csharp
// delete this line, and its CustomAssemblyLoadContext class:
context.LoadUnmanagedLibrary(Path.Combine(Directory.GetCurrentDirectory(), "libwkhtmltox.dll"));
```

**3. Run your app.** The same code makes the PDF. Nothing else changes: `services.AddSingleton(typeof(IConverter), new SynchronizedConverter(new PdfTools()));` stays as it is.

## Make your first PDF

**1. Make a project** and add the package:

```bash
dotnet new console -o Invoice
cd Invoice
dotnet add package Marqius.DinkToPdf
```

**2. Put this** in `Program.cs`:

<!-- example: quickstart Program.cs -->
```csharp
using DinkToPdf;

var converter = new SynchronizedConverter(new PdfTools());
var pdf = converter.Convert(new HtmlToPdfDocument
{
    GlobalSettings = { PaperSize = PaperKind.A4 },
    Objects = { new ObjectSettings { HtmlContent = "<h1>Invoice 42</h1><p>Thank you for your order.</p>" } },
});
File.WriteAllBytes("invoice.pdf", pdf);
```

**3. Run it:**

```bash
dotnet run
```

`invoice.pdf` is in the folder.

## ASP.NET Core

Register one converter for the whole app, as with DinkToPdf:

<!-- example: aspnet register -->
```csharp
using DinkToPdf;
using DinkToPdf.Contracts;

builder.Services.AddSingleton(typeof(IConverter), new SynchronizedConverter(new PdfTools()));
```

Then take `IConverter` in a controller or a minimal API, and return the bytes:

<!-- example: aspnet endpoint -->
```csharp
app.MapGet("/invoice", (IConverter converter) =>
    Results.File(converter.Convert(new HtmlToPdfDocument
    {
        Objects = { new ObjectSettings { HtmlContent = "<h1>Invoice 42</h1>" } },
    }), "application/pdf", "invoice.pdf"));
```

Conversions run side by side, as many at once as the server has processors.

## Docker

`dotnet publish` puts the program in the app's folder: there is nothing to install for it. The .NET images have no fonts, so add one family:

<!-- example: docker Dockerfile -->
```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:8.0
RUN apt-get update && apt-get install -y --no-install-recommends fonts-liberation && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY publish/ .
ENTRYPOINT ["dotnet", "MyApp.dll"]
```

On Alpine (`aspnet:8.0-alpine`), the line is `RUN apk add --no-cache font-liberation`.

## Fonts and languages

Letters are drawn with the fonts installed on the server. When a page has letters no installed font has (Japanese, say), the PDF is still made, and the `Warning` event names the free font that has them, with one command that downloads it:

```
no installed font has those letters. Download the open font that has them, once, and make the PDF again:
    marqius-wkhtmltopdf fonts get "Noto Sans JP"
```

## What is different from DinkToPdf

| | DinkToPdf with libwkhtmltox | Marqius.DinkToPdf |
|---|---|---|
| Native library | libwkhtmltox 0.12.4 (2016), loaded into your app | none: the program runs beside your app |
| Threads | one worker thread for every conversion | side by side, a process each |
| Files on the server (`file:///`, relative paths) | load | load, as in DinkToPdf; set `LoadSettings.BlockLocalFileAccess = true` for HTML you do not control |
| `PdfTools.GetLibraryVersion()` | `0.12.4` | `0.12.6 (marqius-wkhtmltopdf 0.12.14)` |
| Maintained | last release 2017 | yes |

## Questions

**Why do I get "Unable to load shared library 'libwkhtmltox'" with DinkToPdf?** DinkToPdf loads libwkhtmltox, a native library that must sit beside your app, match its 32 or 64 bits, and find Qt's system libraries. Marqius.DinkToPdf loads no native library, so this error cannot happen.

**Why do I get "BadImageFormatException: An attempt was made to load a program with an incorrect format (0x8007000B)"?** A 32-bit libwkhtmltox in a 64-bit process, or the reverse. Marqius.DinkToPdf has no DLL to mismatch.

**Does it work in IIS?** Yes. Each PDF is made by a process of its own, so application pool recycles and concurrent requests are safe. When the pool's identity cannot write the usual cache folder, the program keeps its font index in the temporary folder.

**Publishing for one system?** `dotnet publish -r linux-x64` (or `win-x64`, `linux-arm64`, `osx-arm64`) keeps only that system's program.

## Licence and trial

marqius-wkhtmltopdf is commercial software with a 30-day free trial. After the trial, without a licence, each page carries one line, "Prepared by Marqius, Purchase Licence"; your PDFs never stop. Personal, teaching and open-source use is free with a Community licence. Give a licence in the app's environment as `MARQIUS_LICENCE` (the licence itself, or a file that holds it). [See the plans and prices](https://marqius.com/licence/marqius-wkhtmltopdf).

Marqius.DinkToPdf's own code is MIT, built on [DinkToPdf](https://github.com/rdvojmoc/DinkToPdf) by Rok Dvojmoc (MIT); see [LICENSE.md](LICENSE.md). marqius is not affiliated with DinkToPdf or its author.

The program itself, for Rotativa, NReco.PdfGenerator or your own code, is the package [Marqius.Wkhtmltopdf](https://www.nuget.org/packages/Marqius.Wkhtmltopdf).
