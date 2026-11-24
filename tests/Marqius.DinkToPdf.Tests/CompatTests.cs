using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DinkToPdf;
using DinkToPdf.Contracts;
using Xunit;

namespace Marqius.DinkToPdf.Tests
{
    /// <summary>A converter class of an app's own, that adds something to Convert.</summary>
    public class CountingConverter : SynchronizedConverter
    {
        public int Calls;

        public CountingConverter(ITools tools) : base(tools)
        {
        }

        public override byte[] Convert(IDocument document)
        {
            Interlocked.Increment(ref Calls);
            return base.Convert(document);
        }
    }

    /// <summary>
    /// Code written for DinkToPdf does what it did: relative paths, DinkToPdf's raw calls into
    /// libwkhtmltox, the version text, nothing printed, and handlers that throw.
    /// </summary>
    [Collection("one at a time")]
    public class CompatTests
    {
        public CompatTests()
        {
            Setup.Program();
        }

        [Fact]
        public void Relative_paths_in_settings_mean_the_apps_folder_as_in_DinkToPdf()
        {
            var folder = Directory.CreateDirectory(Path.Combine(Setup.Scratch(), "relative-" + Guid.NewGuid().ToString("N"))).FullName;
            var before = Directory.GetCurrentDirectory();
            try
            {
                File.WriteAllText(Path.Combine(folder, "invoice.html"), "<h1>Invoice 42</h1>");
                File.WriteAllText(Path.Combine(folder, "print.css"), "h1 { color: red }");
                Directory.SetCurrentDirectory(folder);
                var converter = new SynchronizedConverter(new PdfTools());
                var pdf = converter.Convert(new HtmlToPdfDocument
                {
                    GlobalSettings = { Out = "report.pdf" },
                    Objects = { new ObjectSettings { Page = "invoice.html", WebSettings = { UserStyleSheet = "print.css" } } },
                });
                Assert.Empty(pdf); // with Out, the PDF goes to the file, as in DinkToPdf
                var file = Path.Combine(folder, "report.pdf");
                Assert.True(File.Exists(file), "report.pdf is in the app's folder");
                Assert.True(Pdf.Is(File.ReadAllBytes(file)));
            }
            finally
            {
                Directory.SetCurrentDirectory(before);
            }
        }

        [Fact]
        public void DinkToPdfs_raw_calls_make_a_PDF_without_libwkhtmltox()
        {
            Assert.Equal(1, WkHtmlToXBindings.wkhtmltopdf_init(0));
            Assert.Equal(1, WkHtmlToXBindings.wkhtmltopdf_extended_qt());
            Assert.Equal("0.12.6", Marshal.PtrToStringAnsi(WkHtmlToXBindings.wkhtmltopdf_version()));

            var global = WkHtmlToXBindings.wkhtmltopdf_create_global_settings();
            Assert.Equal(1, WkHtmlToXBindings.wkhtmltopdf_set_global_setting(global, "size.paperSize", "A5"));
            Assert.Equal(0, WkHtmlToXBindings.wkhtmltopdf_set_global_setting(global, "no.such.setting", "1"));
            Assert.Equal("A5", GlobalSetting(global, "size.paperSize"));
            var page = WkHtmlToXBindings.wkhtmltopdf_create_object_settings();
            Assert.Equal(1, WkHtmlToXBindings.wkhtmltopdf_set_object_setting(page, "web.defaultEncoding", "utf-8"));

            var converter = WkHtmlToXBindings.wkhtmltopdf_create_converter(global);
            int phases = 0, finished = -1;
            VoidCallback phase = c => phases++;
            IntCallback done = (c, success) => finished = success;
            WkHtmlToXBindings.wkhtmltopdf_set_phase_changed_callback(converter, phase);
            WkHtmlToXBindings.wkhtmltopdf_set_finished_callback(converter, done);
            WkHtmlToXBindings.wkhtmltopdf_add_object(converter, page, "<h1>Raw calls</h1>");
            Assert.True(WkHtmlToXBindings.wkhtmltopdf_convert(converter));

            int length = WkHtmlToXBindings.wkhtmltopdf_get_output(converter, out var data);
            var pdf = new byte[length];
            Marshal.Copy(data, pdf, 0, length);
            Assert.True(Pdf.Is(pdf));
            var (width, height) = Pdf.FirstPageSize(pdf);
            Assert.InRange(width, 418, 421); // A5: 419.5 x 595.3 points
            Assert.InRange(height, 594, 597);
            Assert.Equal(1, finished);
            Assert.True(phases > 0);
            Assert.Equal(6, WkHtmlToXBindings.wkhtmltopdf_phase_count(converter));
            Assert.Equal("Loading pages", Marshal.PtrToStringAnsi(WkHtmlToXBindings.wkhtmltopdf_phase_description(converter, 0)));
            Assert.Equal(0, WkHtmlToXBindings.wkhtmltopdf_http_error_code(converter));
            WkHtmlToXBindings.wkhtmltopdf_destroy_converter(converter);
            Assert.Equal(1, WkHtmlToXBindings.wkhtmltopdf_deinit());
        }

        private static unsafe string GlobalSetting(IntPtr settings, string name)
        {
            // as DinkToPdf's own PdfTools read a setting: a 2048-byte buffer
            var buffer = new byte[2048];
            fixed (byte* b = buffer)
                Assert.Equal(1, WkHtmlToXBindings.wkhtmltopdf_get_global_setting(settings, name, b, buffer.Length));
            return Encoding.UTF8.GetString(buffer, 0, Array.IndexOf(buffer, (byte)0));
        }

        [Fact]
        public void GetLibraryVersion_is_a_plain_version_number_as_libwkhtmltox_gave()
        {
            Assert.Equal(new Version(0, 12, 6), Version.Parse(new PdfTools().GetLibraryVersion()));
        }

        [Fact]
        public void With_no_handlers_a_PDF_made_prints_nothing_and_a_PDF_not_made_prints_why()
        {
            var before = Console.Error;
            var written = new StringWriter();
            Console.SetError(written);
            try
            {
                var converter = new SynchronizedConverter(new PdfTools());
                // a picture that cannot be loaded: a warning, and the PDF is still made
                var pdf = converter.Convert(new HtmlToPdfDocument
                {
                    Objects = { new ObjectSettings { HtmlContent = "<p>Before</p><img src=\"missing.png\"><p>After</p>", LoadSettings = { LoadErrorHandling = ContentErrorHandling.Ignore } } },
                });
                Assert.True(Pdf.Is(pdf));
                Assert.Equal("", written.ToString());

                // a page that is not there: no PDF, and the reason on standard error
                var none = converter.Convert(new HtmlToPdfDocument
                {
                    Objects = { new ObjectSettings { Page = Path.Combine(Setup.Scratch(), "no-such-page-" + Guid.NewGuid().ToString("N") + ".html") } },
                });
                Assert.Empty(none);
                Assert.Contains("DinkToPdf (error)", written.ToString());
            }
            finally
            {
                Console.SetError(before);
            }
        }

        [Fact]
        public void A_handler_that_throws_ends_Convert_with_its_exception_and_never_hangs()
        {
            var converter = new SynchronizedConverter(new PdfTools());
            converter.PhaseChanged += (s, e) => throw new InvalidOperationException("the handler failed");
            var clock = Stopwatch.StartNew();
            var thrown = Assert.Throws<InvalidOperationException>(() => converter.Convert(new HtmlToPdfDocument { Objects = { new ObjectSettings { HtmlContent = "<h1>x</h1>" } } }));
            Assert.Equal("the handler failed", thrown.Message);
            Assert.InRange(clock.Elapsed.TotalSeconds, 0, 30);
        }

        [Fact]
        public async Task ConvertAsync_runs_an_apps_own_Convert_and_cancelling_still_stops_the_program()
        {
            var converter = new CountingConverter(new PdfTools());
            var pdf = await converter.ConvertAsync(new HtmlToPdfDocument { Objects = { new ObjectSettings { HtmlContent = "<h1>x</h1>" } } });
            Assert.True(Pdf.Is(pdf));
            Assert.Equal(1, converter.Calls);
            using (var stop = new CancellationTokenSource(TimeSpan.FromSeconds(1)))
            {
                var clock = Stopwatch.StartNew();
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => converter.ConvertAsync(AsyncTests.Never(), stop.Token));
                Assert.InRange(clock.Elapsed.TotalSeconds, 0.5, 10);
            }
            Assert.Equal(2, converter.Calls);
        }
    }
}
