// One converter, timed the same way for each: built with ENGINE_MARQIUS (Marqius.DinkToPdf),
// ENGINE_ORIGINAL (DinkToPdf 1.0.8 with libwkhtmltox 0.12.4) or ENGINE_PUPPETEER (PuppeteerSharp with
// headless Chromium). tests/bench.py builds and runs each on the same machine, with the same invoice.
//
//   dotnet Bench.dll cold                 the first PDF, timed from the start of Main (the engine started too)
//   dotnet Bench.dll warm 30              the median and slowest of 30 PDFs, one after another
//   dotnet Bench.dll parallel 100 8       100 PDFs, 8 at a time: the total, and PDFs a second
//
// Prints one line of JSON. The invoice is tests/bench/invoice.html (fonts the machine has).
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
#if ENGINE_PUPPETEER
using PuppeteerSharp;
using PuppeteerSharp.Media;
#else
using DinkToPdf;
#endif
#if ENGINE_MARQIUS
using Marqius.DinkToPdf; // ConvertAsync
#endif

public static class Bench
{
    public static async Task<int> Main(string[] args)
    {
        var clock = Stopwatch.StartNew();
        var html = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "invoice.html"));
        var mode = args[0];
        await using var engine = await Engine.Start(args.Length > 3 ? args[3] : null);
        if (mode == "cold")
        {
            var pdf = await engine.Convert(html);
            Report(engine, mode, ("ms", clock.Elapsed.TotalMilliseconds), ("bytes", pdf.Length));
            return pdf.Length > 0 ? 0 : 1;
        }
        await engine.Convert(html); // warm the engine up
        if (mode == "warm")
        {
            var n = int.Parse(args[1]);
            var times = new List<double>();
            for (int i = 0; i < n; i++)
            {
                var one = Stopwatch.StartNew();
                if ((await engine.Convert(html)).Length == 0)
                    return 1;
                times.Add(one.Elapsed.TotalMilliseconds);
            }
            times.Sort();
            Report(engine, mode, ("median_ms", times[n / 2]), ("slowest_ms", times[n - 1]), ("runs", n));
            return 0;
        }
        if (mode == "parallel")
        {
            int n = int.Parse(args[1]), at = int.Parse(args[2]);
            using var slots = new SemaphoreSlim(at);
            var all = Stopwatch.StartNew();
            var made = await Task.WhenAll(Enumerable.Range(0, n).Select(async _ =>
            {
                await slots.WaitAsync();
                try { return (await engine.Convert(html)).Length > 0; }
                finally { slots.Release(); }
            }));
            var total = all.Elapsed.TotalMilliseconds;
            Report(engine, mode, ("total_ms", total), ("pdfs_per_second", n / (total / 1000)), ("made", made.Count(m => m)), ("at_once", at));
            return made.All(m => m) ? 0 : 1;
        }
        return 2;
    }

    private static void Report(Engine engine, string mode, params (string Key, object Value)[] figures)
    {
        var parts = new List<string> { $"\"engine\":\"{engine.Name}\"", $"\"version\":\"{engine.Version}\"", $"\"mode\":\"{mode}\"" };
        foreach (var (k, v) in figures)
            parts.Add($"\"{k}\":{(v is double d ? d.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) : v)}");
        Console.WriteLine("{" + string.Join(",", parts) + "}");
    }
}

public sealed class Engine : IAsyncDisposable
{
#if ENGINE_PUPPETEER
    private IBrowser browser;
    public string Name => "PuppeteerSharp (headless Chromium)";
    public string Version { get; private set; }

    public static async Task<Engine> Start(string chromium)
    {
        // Chromium is downloaded once, beforehand (tests/bench.py), into the folder given here
        var fetcher = new BrowserFetcher(new BrowserFetcherOptions { Path = chromium });
        var installed = await fetcher.DownloadAsync();
        var browser = await Puppeteer.LaunchAsync(new LaunchOptions
        {
            Headless = true,
            ExecutablePath = installed.GetExecutablePath(),
            Args = new[] { "--no-sandbox" },
        });
        return new Engine { browser = browser, Version = $"{typeof(Puppeteer).Assembly.GetName().Version} with {await browser.GetVersionAsync()}" };
    }

    public async Task<byte[]> Convert(string html)
    {
        var page = await browser.NewPageAsync();
        try
        {
            await page.SetContentAsync(html);
            return await page.PdfDataAsync(new PdfOptions { Format = PaperFormat.A4, PrintBackground = true });
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await browser.CloseAsync();
    }
#else
    private SynchronizedConverter converter;
#if ENGINE_MARQIUS
    public string Name => "Marqius.DinkToPdf";
    public string Version => "marqius-wkhtmltopdf " + global::Marqius.Wkhtmltopdf.Wkhtmltopdf.Version;
#else
    public string Name => "DinkToPdf with libwkhtmltox";
    public string Version => $"libwkhtmltox {converter.Tools.GetLibraryVersion()}";
#endif

    public static Task<Engine> Start(string unused)
    {
        // as DinkToPdf's examples do: no Tools.Load() here. The first Convert loads the library on the
        // converter's own thread; loaded on this one, libwkhtmltox hangs when that thread uses it.
        var engine = new Engine { converter = new SynchronizedConverter(new PdfTools()) };
        return Task.FromResult(engine);
    }

    public Task<byte[]> Convert(string html)
    {
        var document = new HtmlToPdfDocument
        {
            GlobalSettings = { PaperSize = PaperKind.A4 },
            Objects = { new ObjectSettings { HtmlContent = html } },
        };
#if ENGINE_MARQIUS
        return converter.ConvertAsync(document);
#else
        // DinkToPdf has no async call: its one worker thread makes each PDF in turn
        return Task.Run(() => converter.Convert(document));
#endif
    }

    public ValueTask DisposeAsync() => default;
#endif
}
