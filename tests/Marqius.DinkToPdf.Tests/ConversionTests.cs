using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DinkToPdf;
using Xunit;

namespace Marqius.DinkToPdf.Tests
{
    /// <summary>
    /// Conversions made by the real program, the way DinkToPdf's users make them: its own example, HTML
    /// with local pictures, several objects, a file named in Out, a failure, and many at once.
    /// </summary>
    public class ConversionTests
    {
        public ConversionTests()
        {
            Setup.Program();
        }

        [Fact]
        public void DinkToPdfs_own_example_makes_a_landscape_A4_with_its_header_and_events()
        {
            var converter = new BasicConverter(new PdfTools());
            var phases = new List<string>();
            var progress = new List<string>();
            bool? finished = null;
            converter.PhaseChanged += (s, e) => phases.Add($"{e.CurrentPhase}/{e.PhaseCount} {e.Description}");
            converter.ProgressChanged += (s, e) => progress.Add(e.Description);
            converter.Finished += (s, e) => finished = e.Success;
            var doc = new HtmlToPdfDocument
            {
                GlobalSettings = { ColorMode = ColorMode.Color, Orientation = Orientation.Landscape, PaperSize = PaperKind.A4 },
                Objects =
                {
                    new ObjectSettings
                    {
                        PagesCount = true,
                        HtmlContent = "<p>Lorem ipsum dolor sit amet, consectetur adipiscing elit.</p>",
                        WebSettings = { DefaultEncoding = "utf-8" },
                        HeaderSettings = { FontSize = 9, Right = "Page [page] of [toPage]", Line = true },
                        FooterSettings = { FontSize = 9, Right = "Page [page] of [toPage]" },
                    },
                },
            };
            var pdf = converter.Convert(doc);
            Assert.True(Pdf.Is(pdf), "a PDF");
            Assert.Equal(1, Pdf.Pages(pdf));
            var (w, h) = Pdf.FirstPageSize(pdf);
            Assert.InRange(w, 841, 843); // A4, turned
            Assert.InRange(h, 594, 596);
            Assert.True(finished);
            Assert.Contains("0/6 Loading pages", phases);
            Assert.Contains("5/6 Printing pages", phases);
            Assert.Contains("Page 1 of 1", progress);
            Assert.Same(doc, converter.ProcessingDocument);
        }

        [Fact]
        public void A_picture_on_this_computer_loads_by_default_and_not_when_blocked()
        {
            var folder = Setup.Scratch();
            var picture = Path.Combine(folder, "red.png");
            File.WriteAllBytes(picture, Png.Red);
            var html = $"<p>Logo:</p><img src=\"{new Uri(picture).AbsoluteUri}\" width=\"40\" height=\"40\">";
            var converter = new SynchronizedConverter(new PdfTools());

            var loaded = converter.Convert(new HtmlToPdfDocument { Objects = { new ObjectSettings { HtmlContent = html } } });
            Assert.True(Pdf.Is(loaded));
            Assert.True(Pdf.HasPicture(loaded), "the local picture is in the PDF");

            var warnings = new List<string>();
            converter.Warning += (s, e) => warnings.Add(e.Message);
            var blocked = converter.Convert(new HtmlToPdfDocument
            {
                Objects = { new ObjectSettings { HtmlContent = html, LoadSettings = { BlockLocalFileAccess = true } } },
            });
            Assert.True(Pdf.Is(blocked), "the page is still made, as wkhtmltopdf makes it");
            Assert.False(Pdf.HasPicture(blocked), "no picture when local files are blocked");
            Assert.Contains(warnings, w => w.Contains("Blocked access to file"));
        }

        [Fact]
        public void Several_objects_make_one_PDF_in_their_order()
        {
            var converter = new BasicConverter(new PdfTools());
            var pdf = converter.Convert(new HtmlToPdfDocument
            {
                GlobalSettings = { PaperSize = PaperKind.Letter },
                Objects =
                {
                    new ObjectSettings { HtmlContent = "<h1>One</h1>" },
                    new ObjectSettings { HtmlContent = "<h1>Two</h1>" },
                    new ObjectSettings { HtmlContent = "<h1>Three</h1>" },
                },
            });
            Assert.True(Pdf.Is(pdf));
            Assert.Equal(3, Pdf.Pages(pdf));
            var (w, h) = Pdf.FirstPageSize(pdf);
            Assert.InRange(w, 611, 613); // Letter
            Assert.InRange(h, 791, 793);
        }

        [Fact]
        public void A_file_named_in_Out_is_written_and_no_bytes_are_returned()
        {
            var output = Path.Combine(Setup.Scratch(), "named.pdf");
            var converter = new BasicConverter(new PdfTools());
            bool? finished = null;
            converter.Finished += (s, e) => finished = e.Success;
            var bytes = converter.Convert(new HtmlToPdfDocument
            {
                GlobalSettings = { Out = output },
                Objects = { new ObjectSettings { HtmlContent = "<p>to a file</p>" } },
            });
            Assert.Empty(bytes);
            Assert.True(finished);
            Assert.True(Pdf.Is(File.ReadAllBytes(output)));
        }

        [Fact]
        public void A_page_that_cannot_be_read_gives_no_bytes_an_error_and_an_unsuccessful_finish()
        {
            var converter = new BasicConverter(new PdfTools());
            var errors = new List<string>();
            bool? finished = null;
            converter.Error += (s, e) => errors.Add(e.Message);
            converter.Finished += (s, e) => finished = e.Success;
            var missing = Path.Combine(Setup.Scratch(), "there-is-no-such-page.html");
            var bytes = converter.Convert(new HtmlToPdfDocument { Objects = { new ObjectSettings { Page = missing } } });
            Assert.Empty(bytes);
            Assert.False(finished);
            Assert.NotEmpty(errors);
        }

        [Fact]
        public void A_document_without_objects_is_refused_as_in_DinkToPdf()
        {
            var converter = new BasicConverter(new PdfTools());
            Assert.Throws<ArgumentException>(() => converter.Convert(new HtmlToPdfDocument()));
        }

        [Fact]
        public void Twenty_conversions_at_once_from_one_shared_converter_all_succeed_with_their_own_events()
        {
            // the way ASP.NET Core apps use DinkToPdf: one SynchronizedConverter for every request
            var converter = new SynchronizedConverter(new PdfTools());
            var finishedFor = new ConcurrentDictionary<object, bool>();
            converter.Finished += (s, e) => finishedFor[e.Document] = e.Success;
            var docs = Enumerable.Range(1, 20).Select(i => new HtmlToPdfDocument
            {
                GlobalSettings = { DocumentTitle = $"Invoice {i}" },
                Objects = { new ObjectSettings { HtmlContent = $"<h1>Invoice {i}</h1>" + string.Concat(Enumerable.Repeat("<p>Line</p>", i)) } },
            }).ToList();
            var results = new byte[docs.Count][];
            Parallel.For(0, docs.Count, new ParallelOptions { MaxDegreeOfParallelism = 20 }, i => results[i] = converter.Convert(docs[i]));
            Assert.All(results, r => Assert.True(Pdf.Is(r)));
            Assert.Equal(20, finishedFor.Count);
            Assert.All(docs, d => Assert.True(finishedFor[d]));
        }

        [Fact]
        public void The_tools_answer_as_a_loaded_patched_wkhtmltopdf()
        {
            var tools = new PdfTools();
            Assert.False(tools.IsLoaded);
            tools.Load();
            Assert.True(tools.IsLoaded);
            Assert.True(tools.ExtendedQt());
            Assert.Equal("0.12.6", tools.GetLibraryVersion()); // a plain version number, as libwkhtmltox gave
        }
    }
}
