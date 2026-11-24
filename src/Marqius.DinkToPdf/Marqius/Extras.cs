using System;
using System.Threading;
using System.Threading.Tasks;
using DinkToPdf;
using DinkToPdf.Contracts;
using Conversion = DinkToPdf.Marqius.Conversion;

// What Marqius.DinkToPdf adds to DinkToPdf lives here, in a namespace of its own: the DinkToPdf
// namespace stays exactly as DinkToPdf 1.0.8 had it, so code written for DinkToPdf compiles and calls
// what it called before, even code with a ConvertAsync of its own.
namespace Marqius.DinkToPdf
{
    /// <summary>
    /// ConvertAsync for any <see cref="IConverter"/>, such as the one ASP.NET Core gives a controller.
    /// Add <c>using Marqius.DinkToPdf;</c> to use it.
    /// </summary>
    public static class ConverterExtensions
    {
        /// <summary>
        /// Converts the document without holding a thread while the PDF is made: the PDF's bytes, or
        /// none when it could not be made, with the same events as <c>Convert</c>. Use it in ASP.NET Core
        /// actions and background services.
        /// </summary>
        /// <example>
        /// <code>
        /// app.MapGet("/invoice", async (IConverter converter, CancellationToken stop) =&gt;
        ///     Results.File(await converter.ConvertAsync(document, stop), "application/pdf", "invoice.pdf"));
        /// </code>
        /// </example>
        /// <param name="converter">The converter: SynchronizedConverter, BasicConverter, or a class of the app's own.</param>
        /// <param name="document">The document to convert.</param>
        /// <param name="cancellationToken">Stops the conversion, and its program, when cancelled
        /// (an ASP.NET Core request's own token stops it when the browser goes away).</param>
        /// <exception cref="OperationCanceledException">The conversion was cancelled.</exception>
        public static Task<byte[]> ConvertAsync(this IConverter converter, IDocument document, CancellationToken cancellationToken = default)
        {
            if (converter == null)
                throw new ArgumentNullException(nameof(converter));
            var type = converter.GetType();
            if (type == typeof(SynchronizedConverter) || type == typeof(BasicConverter))
                return ((BasicConverter)converter).ConvertAsyncCore(document, cancellationToken);
            // a converter of the app's own: its Convert runs (with what it adds), on a thread of the pool;
            // when it reaches DinkToPdf's Convert, cancelling still stops the program
            return Task.Run(() =>
            {
                Conversion.Ambient.Value = cancellationToken;
                return converter.Convert(document);
            }, cancellationToken);
        }
    }
}
