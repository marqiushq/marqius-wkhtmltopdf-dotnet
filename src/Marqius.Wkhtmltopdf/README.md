# wkhtmltopdf for .NET: Marqius.Wkhtmltopdf, the maintained drop-in replacement

[![NuGet](https://img.shields.io/nuget/v/Marqius.Wkhtmltopdf)](https://www.nuget.org/packages/Marqius.Wkhtmltopdf)

**Marqius.Wkhtmltopdf puts marqius-wkhtmltopdf, the maintained drop-in replacement for wkhtmltopdf, in your .NET app** for Windows, Linux and macOS, from one NuGet package. Give its path to the library you use, Rotativa, NReco.PdfGenerator, Wkhtmltopdf.NetCore or your own `Process.Start`, and your HTML becomes PDFs and pictures as before: the same command line and options as wkhtmltopdf 0.12.6, with no Qt, no X server and nothing to install on the server.

- **One package for every server.** Windows x64, Linux x64 and ARM64 (Alpine too), macOS on Apple silicon; `dotnet publish -r linux-x64` keeps only that system's program.
- **Checked before it runs.** Each program is GitHub's own build of the release, checked against its SHA-256 and its GitHub build provenance before it is packed, and against that SHA-256 again before its first run.
- **Many languages, even on one page.** Arabic and Hebrew from right to left, Hindi, Thai, Chinese, Japanese and Korean as well as English.

Using DinkToPdf? [Marqius.DinkToPdf](https://www.nuget.org/packages/Marqius.DinkToPdf) is DinkToPdf on this program, with no libwkhtmltox.

## Make your first PDF

```bash
dotnet new console -o Invoice
cd Invoice
dotnet add package Marqius.Wkhtmltopdf
```

`Program.cs`:

<!-- example: quickstart Program.cs -->
```csharp
using System.Diagnostics;
using Marqius.Wkhtmltopdf;

File.WriteAllText("invoice.html", "<h1>Invoice 42</h1><p>Thank you for your order.</p>");
var run = Process.Start(Wkhtmltopdf.StartInfo(new[] { "invoice.html", "invoice.pdf" }))!;
run.WaitForExit();
Console.WriteLine(run.ExitCode == 0 ? "invoice.pdf is ready" : run.StandardError.ReadToEnd());
```

```bash
dotnet run
```

`Wkhtmltopdf.Path` is the program's full path, for any library that asks where wkhtmltopdf is.

## Rotativa

Rotativa runs `wkhtmltopdf` from a folder of its own. `Wkhtmltopdf.Install` puts that name there, running this program, at the app's start:

```csharp
Marqius.Wkhtmltopdf.Wkhtmltopdf.Install(Path.Combine(app.Environment.WebRootPath, "Rotativa"));
RotativaConfiguration.Setup(app.Environment.WebRootPath, "Rotativa");
```

Delete the old `wkhtmltopdf.exe` from `wwwroot/Rotativa` first, or `Install` keeps it beside the new name as `wkhtmltopdf.before-marqius`.

## Licence and trial

marqius-wkhtmltopdf is commercial software with a 30-day free trial. After the trial, without a licence, each page carries one line, "Prepared by Marqius, Purchase Licence"; your PDFs never stop. Personal, teaching and open-source use is free with a Community licence. Give a licence in the app's environment as `MARQIUS_LICENCE` (the licence itself, or a file that holds it). [See the plans and prices](https://marqius.com/licence/marqius-wkhtmltopdf).

This package's own .NET code is MIT; see LICENSE.md.
