using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using DinkToPdf;
using DinkToPdf.Contracts;
using DinkToPdf.EventDefinitions;

// Code written for DinkToPdf 1.0.8, the kinds of call apps make. tests/customer.py old-build compiles
// it against DinkToPdf 1.0.8 itself (a library from before Marqius.DinkToPdf) and runs it with
// Marqius.DinkToPdf in its place, then compiles it against Marqius.DinkToPdf with every warning as an
// error.
namespace Reports
{
    public static class Invoices
    {
        public static IDocument Document(string number)
        {
            return new HtmlToPdfDocument
            {
                GlobalSettings =
                {
                    ColorMode = ColorMode.Color,
                    Orientation = Orientation.Portrait,
                    PaperSize = PaperKind.A4,
                    Margins = new MarginSettings { Top = 10, Bottom = 10, Left = 10, Right = 10, Unit = Unit.Millimeters },
                    DocumentTitle = "Invoice " + number,
                    DPI = 300,
                    Outline = false,
                    UseCompression = true,
                },
                Objects =
                {
                    new ObjectSettings
                    {
                        PagesCount = true,
                        HtmlContent = "<h1>Invoice " + number + "</h1><p>Thank you for your order.</p>",
                        WebSettings = { DefaultEncoding = "utf-8", PrintMediaType = true, LoadImages = true, EnableJavascript = false, Background = true },
                        HeaderSettings = { FontName = "Arial", FontSize = 9, Right = "Page [page] of [toPage]", Line = true, Spacing = 2.812 },
                        FooterSettings = { FontSize = 8, Center = "Northwind Supplies Ltd" },
                        LoadSettings = { BlockLocalFileAccess = false, ZoomFactor = 1.0, LoadErrorHandling = ContentErrorHandling.Ignore, CustomHeaders = new Dictionary<string, string> { ["X-Invoice"] = number } },
                    },
                },
            };
        }

        public static byte[] Make(IConverter converter, string number, List<string> heard)
        {
            var basic = (BasicConverter)converter;
            EventHandler<PhaseChangedArgs> phase = (s, e) => heard.Add($"phase {e.CurrentPhase + 1} of {e.PhaseCount}: {e.Description}");
            EventHandler<FinishedArgs> finished = (s, e) => heard.Add($"finished: {e.Success}");
            EventHandler<ErrorArgs> error = (s, e) => heard.Add("error: " + e.Message);
            EventHandler<WarningArgs> warning = (s, e) => heard.Add("warning: " + e.Message);
            basic.PhaseChanged += phase;
            basic.Finished += finished;
            basic.Error += error;
            basic.Warning += warning;
            try
            {
                var pdf = converter.Convert(Document(number));
                heard.Add("processing: " + (basic.ProcessingDocument != null));
                heard.Add("library: " + basic.Tools.GetLibraryVersion() + ", extended Qt: " + basic.Tools.ExtendedQt());
                return pdf;
            }
            finally
            {
                basic.PhaseChanged -= phase;
                basic.Finished -= finished;
                basic.Error -= error;
                basic.Warning -= warning;
            }
        }

        // a run on SynchronizedConverter's own thread, as some apps did
        public static int OnTheConverterThread(SynchronizedConverter converter)
        {
            return converter.Invoke(() => Environment.CurrentManagedThreadId);
        }

        // DinkToPdf's raw calls into libwkhtmltox
        public static unsafe string Raw()
        {
            WkHtmlToXBindings.wkhtmltopdf_init(0);
            var global = WkHtmlToXBindings.wkhtmltopdf_create_global_settings();
            WkHtmlToXBindings.wkhtmltopdf_set_global_setting(global, "size.paperSize", "A5");
            var buffer = new byte[2048];
            fixed (byte* b = buffer)
                WkHtmlToXBindings.wkhtmltopdf_get_global_setting(global, "size.paperSize", b, buffer.Length);
            WkHtmlToXBindings.wkhtmltopdf_destroy_global_settings(global);
            var kind = CustomUnmanagedType.LPUTF8Str;
            return Marshal.PtrToStringAnsi(WkHtmlToXBindings.wkhtmltopdf_version()) + ", " + Encoding.UTF8.GetString(buffer, 0, Array.IndexOf(buffer, (byte)0)) + ", " + (int)kind;
        }
    }
}
