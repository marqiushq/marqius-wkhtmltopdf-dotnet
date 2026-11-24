using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using MarqiusProgram = global::Marqius.Wkhtmltopdf.Wkhtmltopdf;

namespace DinkToPdf.Marqius
{
    /// <summary>
    /// One conversion, as libwkhtmltox's converter holds it: the global settings, the objects with their
    /// settings and HTML, the callbacks, and after it ran, the phase, progress and result. It runs as
    /// one run of marqius-wkhtmltopdf, which prints the same phases and progress as wkhtmltopdf.
    /// </summary>
    internal sealed class Conversion
    {
        // libwkhtmltox's phases for a PDF, as wkhtmltopdf's command line numbers them (1/6 .. 6/6)
        private static readonly string[] Phases =
        {
            "Loading pages", "Counting pages", "Loading TOC", "Resolving links", "Loading headers and footers", "Printing pages",
        };

        // as many runs at once as the computer has processors: each is a process of its own
        private static readonly SemaphoreSlim Slots = new SemaphoreSlim(Math.Max(1, Environment.ProcessorCount));

        private static readonly Regex PhaseLine = new Regex(@"^(?<what>.+) \((?<at>\d+)/(?<of>\d+)\)$", RegexOptions.CultureInvariant);
        private static readonly Regex ProgressLine = new Regex(@"^\[[=> ]*\]\s*(?<text>.*)$", RegexOptions.CultureInvariant);
        private static readonly Regex HttpLine = new Regex(@"due to http error: (?<code>\d{3})", RegexOptions.CultureInvariant);

        // the cancellation of a ConvertAsync that runs an app's own Convert (a class of its own that
        // overrides it): its base.Convert reaches here on the same flow, and stops the program on cancel
        internal static readonly AsyncLocal<CancellationToken> Ambient = new AsyncLocal<CancellationToken>();

        internal readonly SettingBag Global;
        internal readonly List<(SettingBag Settings, byte[] Data)> Objects = new List<(SettingBag, byte[])>();

        internal VoidCallback PhaseChanged;
        internal VoidCallback ProgressChanged;
        internal IntCallback Finished;
        internal StringCallback Warning;
        internal StringCallback Error;

        internal int CurrentPhase;
        internal string ProgressString = "";
        internal int HttpErrorCode;
        private bool errorRaised;
        internal byte[] Result = Array.Empty<byte>();

        internal Conversion(SettingBag global)
        {
            Global = global;
        }

        internal static int PhaseCount => Phases.Length;

        internal static string PhaseDescription(int phase)
        {
            return phase >= 0 && phase < Phases.Length ? Phases[phase] : "";
        }

        /// <summary>The command line for this conversion; data objects are written to files in folder.</summary>
        internal List<string> CommandLine(string folder, out string output)
        {
            var args = new List<string>();
            Arguments.Global(Global, args);
            int n = 0;
            foreach (var (settings, data) in Objects)
            {
                n++;
                if (settings.Has("isTableOfContent", out var toc) && Arguments.IsTrue(toc))
                {
                    args.Add("toc");
                }
                else
                {
                    string input;
                    if (settings.Has("page", out var page) && page.Length > 0)
                    {
                        input = page;
                    }
                    else
                    {
                        // HTML given as data: libwkhtmltox reads it as UTF-8, and so does the program
                        // from a file that starts with the UTF-8 mark
                        input = Path.Combine(folder, $"object-{n}.html");
                        using (var f = File.Create(input))
                        {
                            var bytes = data ?? Array.Empty<byte>();
                            if (!(bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF))
                                f.Write(new byte[] { 0xEF, 0xBB, 0xBF }, 0, 3);
                            f.Write(bytes, 0, bytes.Length);
                        }
                    }
                    args.Add("page");
                    args.Add(input);
                }
                Arguments.Object(settings, args);
                // libwkhtmltox 0.12.4, which DinkToPdf carries, lets pages load files from this
                // computer unless told not to; wkhtmltopdf 0.12.6 blocks them unless told to
                if (!settings.Has("load.blockLocalFileAccess", out _))
                    args.Add("--enable-local-file-access");
            }
            output = Global.Has("out", out var path) && path.Length > 0 ? path : null;
            args.Add(output ?? "-");
            return args;
        }

        /// <summary>Runs it: true when the PDF was made, as libwkhtmltox's convert says.</summary>
        internal bool Run(IntPtr handle)
        {
            return RunAsync(handle, Ambient.Value).ConfigureAwait(false).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Runs it without holding a thread while the program works. Cancelling stops the program and
        /// throws <see cref="OperationCanceledException"/>; going past <see cref="PdfTools.Timeout"/>
        /// stops it too, and ends as a failed conversion (an error, and no PDF).
        /// </summary>
        internal async Task<bool> RunAsync(IntPtr handle, CancellationToken cancel)
        {
            if (Objects.Count == 0)
            {
                RaiseError(handle, "No objects to convert");
                Finished?.Invoke(handle, 0);
                return false;
            }
            var folder = Path.Combine(Path.GetTempPath(), "marqius-dinktopdf-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            await Slots.WaitAsync(cancel).ConfigureAwait(false);
            try
            {
                var args = CommandLine(folder, out var output);
                var start = MarqiusProgram.StartInfo(args);
                // the app's own folder, as libwkhtmltox ran in the app: relative paths in the settings
                // (Out = "report.pdf", Page = "invoice.html", a style sheet) mean what they meant there
                start.WorkingDirectory = AppFolder() ?? folder;
                byte[] stdout;
                int exit;
                var limit = PdfTools.Timeout;
                using (var process = new Process { StartInfo = start, EnableRaisingEvents = true })
                using (var timer = limit.HasValue ? new CancellationTokenSource(limit.Value) : new CancellationTokenSource())
                using (var either = CancellationTokenSource.CreateLinkedTokenSource(cancel, timer.Token))
                {
                    var exited = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                    process.Exited += (s, e) => exited.TrySetResult(true);
                    process.Start();
                    process.StandardInput.Close();
                    var collected = new MemoryStream();
                    var copy = process.StandardOutput.BaseStream.CopyToAsync(collected);
                    var messages = ReadMessagesAsync(process.StandardError, handle);
                    using (either.Token.Register(() => exited.TrySetCanceled()))
                    {
                        try
                        {
                            await exited.Task.ConfigureAwait(false);
                        }
                        catch (OperationCanceledException)
                        {
                            Stop(process);
                            try
                            {
                                await Task.WhenAll(copy, messages).ConfigureAwait(false);
                            }
                            catch (Exception)
                            {
                                // the pipes closed with the program
                            }
                            if (cancel.IsCancellationRequested)
                                throw new OperationCanceledException("The conversion was cancelled, and marqius-wkhtmltopdf stopped.", cancel);
                            RaiseError(handle, $"marqius-wkhtmltopdf took longer than {limit.Value.TotalSeconds:0.#} s (PdfTools.Timeout) and was stopped");
                            CurrentPhase = PhaseCount - 1;
                            Finished?.Invoke(handle, 0);
                            Result = Array.Empty<byte>();
                            return false;
                        }
                    }
                    await Task.WhenAll(copy, messages).ConfigureAwait(false);
                    process.WaitForExit();
                    exit = process.ExitCode;
                    stdout = collected.ToArray();
                    // a handler that threw: the program was let finish, and the exception goes to the caller
                    handlerFailure?.Throw();
                }
                bool made;
                if (output == null)
                {
                    made = IsPdf(stdout);
                    Result = made ? stdout : Array.Empty<byte>();
                }
                else
                {
                    // written to the file named in "out": libwkhtmltox gives no bytes back then
                    made = File.Exists(output) && new FileInfo(output).Length > 0;
                    Result = Array.Empty<byte>();
                }
                if (!made && !errorRaised)
                    RaiseError(handle, $"marqius-wkhtmltopdf ended with exit status {exit} and made no PDF");
                CurrentPhase = PhaseCount - 1;
                Finished?.Invoke(handle, made ? 1 : 0);
                return made;
            }
            finally
            {
                Slots.Release();
                try
                {
                    Directory.Delete(folder, true);
                }
                catch (Exception)
                {
                    // a file still open on Windows: the temporary folder's cleaning takes it later
                }
            }
        }

        private void RaiseError(IntPtr handle, string message)
        {
            errorRaised = true;
            Error?.Invoke(handle, message);
        }

        // the first exception an app's handler threw while the program's messages were read
        private System.Runtime.ExceptionServices.ExceptionDispatchInfo handlerFailure;

        private void Raise(Action callback)
        {
            if (handlerFailure != null)
                return; // after a handler threw, the messages are only drained, so the program can end
            try
            {
                callback();
            }
            catch (Exception e)
            {
                handlerFailure = System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(e);
            }
        }

        private static string AppFolder()
        {
            try
            {
                var here = Directory.GetCurrentDirectory();
                return Directory.Exists(here) ? here : null;
            }
            catch (Exception)
            {
                return null; // the app's folder was removed: the program runs in the conversion's own
            }
        }

        private static bool IsPdf(byte[] bytes)
        {
            return bytes.Length > 8 && bytes[0] == '%' && bytes[1] == 'P' && bytes[2] == 'D' && bytes[3] == 'F' && bytes[4] == '-';
        }

        // Stops the program (and a browser it started for a page's scripts, where .NET can say so).
        private static void Stop(Process process)
        {
            try
            {
#if NET8_0_OR_GREATER
                process.Kill(entireProcessTree: true);
#else
                process.Kill();
#endif
            }
            catch (Exception)
            {
                // it ended by itself in the meantime
            }
        }

        // The program's messages, as they come: phases and progress go to their callbacks, warnings
        // and errors to theirs. A line indented under a warning belongs to it (the command that
        // downloads missing fonts, for instance).
        private async Task ReadMessagesAsync(StreamReader stderr, IntPtr handle)
        {
            var line = new StringBuilder();
            string pending = null;
            bool pendingIsError = false;

            void Flush()
            {
                if (pending == null)
                    return;
                var message = pending.TrimEnd();
                pending = null;
                if (pendingIsError)
                    Raise(() => RaiseError(handle, message));
                else
                    Raise(() => Warning?.Invoke(handle, message));
            }

            void Take(string text)
            {
                var trimmed = text.TrimEnd();
                if (trimmed.Length == 0)
                    return;
                if (pending != null && (text.StartsWith(" ", StringComparison.Ordinal) || text.StartsWith("\t", StringComparison.Ordinal)))
                {
                    pending += "\n" + trimmed.Trim();
                    return;
                }
                Flush();
                Match m;
                if ((m = PhaseLine.Match(trimmed)).Success && int.TryParse(m.Groups["at"].Value, out var at))
                {
                    CurrentPhase = Math.Max(0, Math.Min(PhaseCount - 1, at - 1));
                    ProgressString = "";
                    Raise(() => PhaseChanged?.Invoke(handle));
                }
                else if ((m = ProgressLine.Match(trimmed)).Success)
                {
                    ProgressString = m.Groups["text"].Value.Trim();
                    Raise(() => ProgressChanged?.Invoke(handle));
                }
                else if (trimmed == "Done")
                {
                    // the end of a successful run
                }
                else if (trimmed.StartsWith("Exit with code ", StringComparison.Ordinal))
                {
                    // the command line's summary of what it already said; libwkhtmltox never sent it,
                    // but keeps the HTTP status that stopped the page
                    if ((m = HttpLine.Match(trimmed)).Success)
                        HttpErrorCode = int.Parse(m.Groups["code"].Value, System.Globalization.CultureInfo.InvariantCulture);
                }
                else if (trimmed.StartsWith("Error: ", StringComparison.Ordinal))
                {
                    pending = trimmed.Substring("Error: ".Length);
                    pendingIsError = true;
                }
                else
                {
                    pending = trimmed.StartsWith("Warning: ", StringComparison.Ordinal) ? trimmed.Substring("Warning: ".Length) : trimmed;
                    pendingIsError = false;
                }
            }

            var buffer = new char[4096];
            int read;
            while ((read = await stderr.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false)) > 0)
            {
                for (int i = 0; i < read; i++)
                {
                    var c = buffer[i];
                    if (c == '\r' || c == '\n')
                    {
                        if (line.Length > 0)
                            Take(line.ToString());
                        line.Clear();
                    }
                    else
                    {
                        line.Append(c);
                    }
                }
            }
            if (line.Length > 0)
                Take(line.ToString());
            Flush();
        }
    }
}
