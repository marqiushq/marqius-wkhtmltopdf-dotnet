using System;
using System.Collections.Concurrent;
using System.Text;
using System.Threading;
using DinkToPdf.Contracts;
using DinkToPdf.Marqius;
using MarqiusProgram = global::Marqius.Wkhtmltopdf.Wkhtmltopdf;

namespace DinkToPdf
{
    /// <summary>
    /// DinkToPdf's tools, with marqius-wkhtmltopdf in place of libwkhtmltox: the same calls, settings
    /// and callbacks, and each conversion made by one run of the program. There is no native library to
    /// load, so this class is safe to use from any thread, and conversions may run side by side.
    /// </summary>
    public sealed class PdfTools : ITools
    {
        // what libwkhtmltox's pointers pointed at: settings and converters, by a number of their own
        private static readonly ConcurrentDictionary<long, object> Handles = new ConcurrentDictionary<long, object>();
        private static long last;

        /// <summary>True once <see cref="Load"/> found marqius-wkhtmltopdf for this computer.</summary>
        public bool IsLoaded { get; private set; }

        /// <summary>Tools for converters: <c>new SynchronizedConverter(new PdfTools())</c>.</summary>
        public PdfTools()
        {
            IsLoaded = false;
        }

        /// <summary>Finds marqius-wkhtmltopdf for this computer (and makes it runnable).</summary>
        public void Load()
        {
            if (IsLoaded)
                return;
            _ = MarqiusProgram.Path;
            IsLoaded = true;
        }

        /// <summary>True: marqius-wkhtmltopdf has everything the patched Qt gave wkhtmltopdf (headers, footers, outlines, links).</summary>
        public bool ExtendedQt() => true;

        /// <summary>The wkhtmltopdf version marqius-wkhtmltopdf answers as, with its own version.</summary>
        public string GetLibraryVersion() => $"0.12.6 (marqius-wkhtmltopdf {MarqiusProgram.Version})";

        /// <inheritdoc/>
        public IntPtr CreateGlobalSettings() => Add(new SettingBag(global: true));

        /// <inheritdoc/>
        public int SetGlobalSetting(IntPtr settings, string name, string value) => Get<SettingBag>(settings).Set(name, value) ? 1 : 0;

        /// <inheritdoc/>
        public string GetGlobalSetting(IntPtr settings, string name) => Get<SettingBag>(settings).Get(name);

        /// <inheritdoc/>
        public void DestroyGlobalSetting(IntPtr settings) => Remove(settings);

        /// <inheritdoc/>
        public IntPtr CreateObjectSettings() => Add(new SettingBag(global: false));

        /// <inheritdoc/>
        public int SetObjectSetting(IntPtr settings, string name, string value) => Get<SettingBag>(settings).Set(name, value) ? 1 : 0;

        /// <inheritdoc/>
        public string GetObjectSetting(IntPtr settings, string name) => Get<SettingBag>(settings).Get(name);

        /// <inheritdoc/>
        public void DestroyObjectSetting(IntPtr settings) => Remove(settings);

        /// <inheritdoc/>
        public IntPtr CreateConverter(IntPtr globalSettings)
        {
            var global = Get<SettingBag>(globalSettings);
            Remove(globalSettings); // the converter owns them now, as in libwkhtmltox
            return Add(new Conversion(global));
        }

        /// <inheritdoc/>
        public void AddObject(IntPtr converter, IntPtr objectSettings, byte[] data)
        {
            var settings = Get<SettingBag>(objectSettings);
            Remove(objectSettings);
            Get<Conversion>(converter).Objects.Add((settings, data));
        }

        /// <inheritdoc/>
        public void AddObject(IntPtr converter, IntPtr objectSettings, string data)
        {
            AddObject(converter, objectSettings, data == null ? null : Encoding.UTF8.GetBytes(data));
        }

        /// <inheritdoc/>
        public bool DoConversion(IntPtr converter) => Get<Conversion>(converter).Run(converter);

        /// <summary>The conversion, without holding a thread while the program works.</summary>
        internal System.Threading.Tasks.Task<bool> DoConversionAsync(IntPtr converter, System.Threading.CancellationToken cancellationToken) =>
            Get<Conversion>(converter).RunAsync(converter, cancellationToken);

        /// <summary>
        /// The longest one conversion may take before marqius-wkhtmltopdf is stopped, and the
        /// conversion ends with an error and no PDF. Null (the default) waits as long as it takes, as
        /// DinkToPdf did. Applies to every converter in the app.
        /// </summary>
        /// <example><code>PdfTools.Timeout = TimeSpan.FromSeconds(30);</code></example>
        public static TimeSpan? Timeout { get; set; }

        /// <inheritdoc/>
        public void DestroyConverter(IntPtr converter) => Remove(converter);

        /// <inheritdoc/>
        public byte[] GetConversionResult(IntPtr converter) => Get<Conversion>(converter).Result;

        /// <inheritdoc/>
        public int SetPhaseChangedCallback(IntPtr converter, VoidCallback callback)
        {
            Get<Conversion>(converter).PhaseChanged = callback;
            return 1;
        }

        /// <inheritdoc/>
        public int SetProgressChangedCallback(IntPtr converter, VoidCallback callback)
        {
            Get<Conversion>(converter).ProgressChanged = callback;
            return 1;
        }

        /// <inheritdoc/>
        public int SetFinishedCallback(IntPtr converter, IntCallback callback)
        {
            Get<Conversion>(converter).Finished = callback;
            return 1;
        }

        /// <inheritdoc/>
        public int SetWarningCallback(IntPtr converter, StringCallback callback)
        {
            Get<Conversion>(converter).Warning = callback;
            return 1;
        }

        /// <inheritdoc/>
        public int SetErrorCallback(IntPtr converter, StringCallback callback)
        {
            Get<Conversion>(converter).Error = callback;
            return 1;
        }

        /// <inheritdoc/>
        public int GetPhaseCount(IntPtr converter) => Conversion.PhaseCount;

        /// <inheritdoc/>
        public int GetCurrentPhase(IntPtr converter) => Get<Conversion>(converter).CurrentPhase;

        /// <inheritdoc/>
        public string GetPhaseDescription(IntPtr converter, int phase) => Conversion.PhaseDescription(phase);

        /// <inheritdoc/>
        public string GetProgressString(IntPtr converter) => Get<Conversion>(converter).ProgressString;

        /// <inheritdoc/>
        public void Dispose()
        {
            // nothing to unload: there is no native library
        }

        // for the tests: the conversion behind a converter handle
        internal static Conversion ConversionOf(IntPtr converter) => Get<Conversion>(converter);

        private static IntPtr Add(object o)
        {
            var id = Interlocked.Increment(ref last);
            Handles[id] = o;
            return new IntPtr(id);
        }

        private static T Get<T>(IntPtr handle) where T : class
        {
            if (Handles.TryGetValue(handle.ToInt64(), out var o) && o is T t)
                return t;
            throw new ArgumentException($"No {typeof(T).Name} has the handle {handle}: it was destroyed, or never made.", nameof(handle));
        }

        private static void Remove(IntPtr handle)
        {
            Handles.TryRemove(handle.ToInt64(), out _);
        }
    }
}
