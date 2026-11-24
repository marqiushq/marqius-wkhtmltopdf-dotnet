using System.Threading;
using System.Threading.Tasks;
using DinkToPdf.Contracts;

namespace DinkToPdf
{
    /// <summary>
    /// Calls for any <see cref="IConverter"/>, such as the one ASP.NET Core gives a controller.
    /// </summary>
    public static class ConverterExtensions
    {
        /// <summary>
        /// Converts the document without holding a thread while the PDF is made: the PDF's bytes, or
        /// none when it could not be made. Use it in ASP.NET Core actions and background services.
        /// </summary>
        /// <example>
        /// <code>
        /// app.MapGet("/invoice", async (IConverter converter, CancellationToken stop) =&gt;
        ///     Results.File(await converter.ConvertAsync(document, stop), "application/pdf", "invoice.pdf"));
        /// </code>
        /// </example>
        /// <param name="converter">The converter: SynchronizedConverter or BasicConverter.</param>
        /// <param name="document">The document to convert.</param>
        /// <param name="cancellationToken">Stops the conversion, and its program, when cancelled
        /// (an ASP.NET Core request's own token stops it when the browser goes away).</param>
        /// <exception cref="System.OperationCanceledException">The conversion was cancelled.</exception>
        public static Task<byte[]> ConvertAsync(this IConverter converter, IDocument document, CancellationToken cancellationToken = default)
        {
            if (converter is BasicConverter basic)
                return basic.ConvertAsync(document, cancellationToken);
            return Task.Run(() => converter.Convert(document), cancellationToken);
        }
    }
}
