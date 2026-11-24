using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using DinkToPdf;
using DinkToPdf.Contracts;
using Xunit;

namespace Marqius.DinkToPdf.Tests
{
    // a page that never says it is ready: wkhtmltopdf (and marqius-wkhtmltopdf) wait for it for ever
    public class WaitingObject : ObjectSettings
    {
        [WkHtml("load.windowStatus")]
        public string WindowStatus { get; set; }
    }

    [CollectionDefinition("one at a time", DisableParallelization = true)]
    public class OneAtATime
    {
    }

    /// <summary>ConvertAsync, cancelling it, and PdfTools.Timeout.</summary>
    [Collection("one at a time")]
    public class AsyncTests
    {
        public AsyncTests()
        {
            Setup.Program();
        }

        private static HtmlToPdfDocument Never() => new HtmlToPdfDocument
        {
            Objects = { new WaitingObject { HtmlContent = "<p>This page never says it is ready.</p>", WindowStatus = "ready" } },
        };

        [Fact]
        public async Task ConvertAsync_makes_the_same_PDF_as_Convert_with_its_events()
        {
            IConverter converter = new SynchronizedConverter(new PdfTools());
            bool? finished = null;
            converter.Finished += (s, e) => finished = e.Success;
            var pdf = await converter.ConvertAsync(new HtmlToPdfDocument { Objects = { new ObjectSettings { HtmlContent = "<h1>Async</h1>" } } });
            Assert.True(Pdf.Is(pdf));
            Assert.Equal(1, Pdf.Pages(pdf));
            Assert.True(finished);
        }

        [Fact]
        public async Task Cancelling_stops_the_program_at_once()
        {
            var converter = new SynchronizedConverter(new PdfTools());
            using (var stop = new CancellationTokenSource(TimeSpan.FromSeconds(1)))
            {
                var clock = Stopwatch.StartNew();
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => converter.ConvertAsync(Never(), stop.Token));
                Assert.InRange(clock.Elapsed.TotalSeconds, 0.5, 10);
            }
        }

        [Fact]
        public void A_timeout_stops_a_conversion_that_takes_too_long_and_says_why()
        {
            var converter = new BasicConverter(new PdfTools());
            var errors = new List<string>();
            bool? finished = null;
            converter.Error += (s, e) => errors.Add(e.Message);
            converter.Finished += (s, e) => finished = e.Success;
            PdfTools.Timeout = TimeSpan.FromSeconds(1);
            try
            {
                var clock = Stopwatch.StartNew();
                var pdf = converter.Convert(Never());
                Assert.InRange(clock.Elapsed.TotalSeconds, 0.5, 10);
                Assert.Empty(pdf);
                Assert.False(finished);
                Assert.Contains(errors, e => e.Contains("PdfTools.Timeout"));
            }
            finally
            {
                PdfTools.Timeout = null;
            }
        }

        [Fact]
        public async Task Fifty_async_conversions_at_once_all_succeed()
        {
            var converter = new SynchronizedConverter(new PdfTools());
            var tasks = new List<Task<byte[]>>();
            for (int i = 1; i <= 50; i++)
            {
                tasks.Add(converter.ConvertAsync(new HtmlToPdfDocument { Objects = { new ObjectSettings { HtmlContent = $"<h1>Invoice {i}</h1>" } } }));
            }
            var all = await Task.WhenAll(tasks);
            Assert.All(all, pdf => Assert.True(Pdf.Is(pdf)));
        }
    }
}
