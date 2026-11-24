using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using DinkToPdf;
using Xunit;

namespace Marqius.DinkToPdf.Tests
{
    /// <summary>
    /// Every setting DinkToPdf has reaches marqius-wkhtmltopdf's command line as wkhtmltopdf spells it.
    /// The settings go the way DinkToPdf sends them (BasicConverter names each to the tools).
    /// </summary>
    public class ArgumentsTests
    {
        // the command line BasicConverter's settings make, read before anything runs
        private static List<string> CommandLine(HtmlToPdfDocument document, out string folder)
        {
            var tools = new PdfTools();
            var converter = new BasicConverter(tools);
            var create = typeof(BasicConverter).GetMethod("CreateConverter", BindingFlags.Instance | BindingFlags.NonPublic);
            var handle = (IntPtr)create.Invoke(converter, new object[] { document });
            folder = Setup.Scratch();
            try
            {
                return PdfTools.ConversionOf(handle).CommandLine(folder, out _);
            }
            finally
            {
                tools.DestroyConverter(handle);
            }
        }

        private static void Has(List<string> args, params string[] run)
        {
            for (int i = 0; i + run.Length <= args.Count; i++)
            {
                if (args.Skip(i).Take(run.Length).SequenceEqual(run))
                    return;
            }
            Assert.Fail($"[{string.Join(" ", run)}] is not in: {string.Join(" ", args)}");
        }

        [Fact]
        public void Global_settings_each_become_their_option()
        {
            var doc = new HtmlToPdfDocument
            {
                GlobalSettings =
                {
                    Orientation = Orientation.Landscape,
                    ColorMode = ColorMode.Grayscale,
                    UseCompression = false,
                    DPI = 150,
                    PageOffset = 2,
                    Copies = 3,
                    Collate = true,
                    Outline = true,
                    OutlineDepth = 2,
                    DumpOutline = "outline.xml",
                    DocumentTitle = "Invoice 42",
                    ImageDPI = 300,
                    ImageQuality = 90,
                    CookieJar = "jar.txt",
                    PaperSize = PaperKind.A5,
                    Margins = new MarginSettings { Top = 10, Bottom = 12.5, Left = 5, Right = 6, Unit = Unit.Millimeters },
                    ViewportSize = "1280x1024",
                },
                Objects = { new ObjectSettings { HtmlContent = "<p>x</p>" } },
            };
            var args = CommandLine(doc, out _);
            Has(args, "--orientation", "Landscape");
            Has(args, "--grayscale");
            Has(args, "--no-pdf-compression");
            Has(args, "--dpi", "150");
            Has(args, "--page-offset", "2");
            Has(args, "--copies", "3");
            Has(args, "--collate");
            Has(args, "--outline");
            Has(args, "--outline-depth", "2");
            Has(args, "--dump-outline", "outline.xml");
            Has(args, "--title", "Invoice 42");
            Has(args, "--image-dpi", "300");
            Has(args, "--image-quality", "90");
            Has(args, "--cookie-jar", "jar.txt");
            Has(args, "--page-width", "148mm");
            Has(args, "--page-height", "210mm");
            Has(args, "--margin-top", "10mm");
            Has(args, "--margin-bottom", "12.5mm");
            Has(args, "--margin-left", "5mm");
            Has(args, "--margin-right", "6mm");
            Has(args, "--viewport-size", "1280x1024");
            Assert.Equal("-", args.Last());
        }

        [Fact]
        public void Object_settings_each_become_their_option()
        {
            var doc = new HtmlToPdfDocument
            {
                Objects =
                {
                    new ObjectSettings
                    {
                        Page = "https://example.com/invoice",
                        UseExternalLinks = false,
                        UseLocalLinks = true,
                        ProduceForms = true,
                        IncludeInOutline = false,
                        PagesCount = true,
                        WebSettings =
                        {
                            Background = false,
                            LoadImages = false,
                            EnableJavascript = false,
                            EnableIntelligentShrinking = false,
                            MinimumFontSize = 8,
                            PrintMediaType = true,
                            DefaultEncoding = "utf-8",
                            UserStyleSheet = "print.css",
                            enablePlugins = false,
                        },
                        HeaderSettings = { FontSize = 9, FontName = "Arial", Left = "[title]", Center = "Invoice", Right = "Page [page] of [toPage]", Line = true, Spacing = 1.8, HtmUrl = "header.html" },
                        FooterSettings = { FontSize = 8, FontName = "Times", Left = "L", Center = "C", Right = "R", Line = false, Spacing = 2, HtmUrl = "footer.html" },
                        LoadSettings =
                        {
                            Username = "ann",
                            Password = "secret",
                            JSDelay = 500,
                            ZoomFactor = 1.25,
                            BlockLocalFileAccess = true,
                            StopSlowScript = false,
                            DebugJavascript = true,
                            LoadErrorHandling = ContentErrorHandling.Ignore,
                            Proxy = "http://proxy:3128",
                            CustomHeaders = new Dictionary<string, string> { ["X-Tenant"] = "7", ["Accept-Language"] = "fr" },
                            RepeatCustomHeaders = true,
                            Cookies = new Dictionary<string, string> { ["session"] = "abc" },
                        },
                    },
                },
            };
            var args = CommandLine(doc, out _);
            Has(args, "page", "https://example.com/invoice");
            Has(args, "--disable-external-links");
            Has(args, "--enable-internal-links");
            Has(args, "--enable-forms");
            Has(args, "--exclude-from-outline");
            Has(args, "--no-background");
            Has(args, "--no-images");
            Has(args, "--disable-javascript");
            Has(args, "--disable-smart-shrinking");
            Has(args, "--minimum-font-size", "8");
            Has(args, "--print-media-type");
            Has(args, "--encoding", "utf-8");
            Has(args, "--user-style-sheet", "print.css");
            Has(args, "--disable-plugins");
            Has(args, "--header-font-size", "9");
            Has(args, "--header-font-name", "Arial");
            Has(args, "--header-left", "[title]");
            Has(args, "--header-center", "Invoice");
            Has(args, "--header-right", "Page [page] of [toPage]");
            Has(args, "--header-line");
            Has(args, "--header-spacing", "1.8");
            Has(args, "--header-html", "header.html");
            Has(args, "--footer-font-size", "8");
            Has(args, "--footer-font-name", "Times");
            Has(args, "--footer-left", "L");
            Has(args, "--footer-center", "C");
            Has(args, "--footer-right", "R");
            Has(args, "--no-footer-line");
            Has(args, "--footer-spacing", "2");
            Has(args, "--footer-html", "footer.html");
            Has(args, "--username", "ann");
            Has(args, "--password", "secret");
            Has(args, "--javascript-delay", "500");
            Has(args, "--zoom", "1.25");
            Has(args, "--disable-local-file-access");
            Has(args, "--no-stop-slow-scripts");
            Has(args, "--debug-javascript");
            Has(args, "--load-error-handling", "ignore");
            Has(args, "--proxy", "http://proxy:3128");
            Has(args, "--custom-header", "X-Tenant", "7");
            Has(args, "--custom-header", "Accept-Language", "fr");
            Has(args, "--custom-header-propagation");
            Has(args, "--cookie", "session", "abc");
            Assert.DoesNotContain("--enable-local-file-access", args);
        }

        [Fact]
        public void Files_on_this_computer_load_unless_blocked_as_in_DinkToPdf()
        {
            // DinkToPdf carries libwkhtmltox 0.12.4, which loads local files unless told not to
            var args = CommandLine(new HtmlToPdfDocument { Objects = { new ObjectSettings { HtmlContent = "<p>x</p>" } } }, out var folder);
            Assert.Equal(new[] { "page", Path.Combine(folder, "object-1.html"), "--enable-local-file-access", "-" }, args);
        }

        [Fact]
        public void Html_content_is_written_as_utf8_with_its_mark()
        {
            var html = "<p>Grüße, naïve café – ‘quoted’ €12</p>";
            CommandLine(new HtmlToPdfDocument { Objects = { new ObjectSettings { HtmlContent = html } } }, out var folder);
            var bytes = File.ReadAllBytes(Path.Combine(folder, "object-1.html"));
            Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, bytes.Take(3));
            Assert.Equal(html, System.Text.Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3));
        }

        [Fact]
        public void Each_object_keeps_its_own_options()
        {
            var doc = new HtmlToPdfDocument
            {
                Objects =
                {
                    new ObjectSettings { HtmlContent = "<p>cover</p>", WebSettings = { Background = false } },
                    new ObjectSettings { Page = "https://example.com/terms", HeaderSettings = { Center = "Terms" } },
                },
            };
            var args = CommandLine(doc, out var folder);
            var second = args.IndexOf("https://example.com/terms");
            Assert.True(args.IndexOf("--no-background") < second, string.Join(" ", args));
            Assert.True(args.IndexOf("--header-center") > second, string.Join(" ", args));
            Assert.Equal("page", args[second - 1]);
        }

        [Fact]
        public void The_out_setting_names_the_file_in_place_of_standard_output()
        {
            var doc = new HtmlToPdfDocument { GlobalSettings = { Out = "invoice.pdf" }, Objects = { new ObjectSettings { HtmlContent = "<p>x</p>" } } };
            var args = CommandLine(doc, out _);
            Assert.Equal("invoice.pdf", args.Last());
            Assert.DoesNotContain("-", args);
        }

        [Fact]
        public void Unknown_settings_are_refused_as_libwkhtmltox_refuses_them()
        {
            var tools = new PdfTools();
            var global = tools.CreateGlobalSettings();
            Assert.Equal(1, tools.SetGlobalSetting(global, "orientation", "Landscape"));
            Assert.Equal(0, tools.SetGlobalSetting(global, "no.such.setting", "1"));
            Assert.Equal("Landscape", tools.GetGlobalSetting(global, "orientation"));
            var obj = tools.CreateObjectSettings();
            Assert.Equal(1, tools.SetObjectSetting(obj, "web.loadImages", "false"));
            Assert.Equal(0, tools.SetObjectSetting(obj, "orientation", "Landscape"));
            tools.DestroyGlobalSetting(global);
            tools.DestroyObjectSetting(obj);
        }

        [Theory]
        [InlineData("plain", "plain")]
        [InlineData("two words", "\"two words\"")]
        [InlineData("", "\"\"")]
        [InlineData("say \"hi\"", "\"say \\\"hi\\\"\"")]
        [InlineData(@"C:\Program Files\x\", "\"C:\\Program Files\\x\\\\\"")]
        public void Arguments_are_quoted_as_Windows_and_dotnet_read_them(string argument, string quoted)
        {
            Assert.Equal(quoted, global::Marqius.Wkhtmltopdf.Wkhtmltopdf.JoinArguments(new[] { argument }));
        }
    }
}
