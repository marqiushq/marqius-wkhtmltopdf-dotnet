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

        public bool IsLoaded { get; private set; }

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

        public IntPtr CreateGlobalSettings() => Add(new SettingBag(global: true));

        public int SetGlobalSetting(IntPtr settings, string name, string value) => Get<SettingBag>(settings).Set(name, value) ? 1 : 0;

        public string GetGlobalSetting(IntPtr settings, string name) => Get<SettingBag>(settings).Get(name);

        public void DestroyGlobalSetting(IntPtr settings) => Remove(settings);

        public IntPtr CreateObjectSettings() => Add(new SettingBag(global: false));

        public int SetObjectSetting(IntPtr settings, string name, string value) => Get<SettingBag>(settings).Set(name, value) ? 1 : 0;

        public string GetObjectSetting(IntPtr settings, string name) => Get<SettingBag>(settings).Get(name);

        public void DestroyObjectSetting(IntPtr settings) => Remove(settings);

        public IntPtr CreateConverter(IntPtr globalSettings)
        {
            var global = Get<SettingBag>(globalSettings);
            Remove(globalSettings); // the converter owns them now, as in libwkhtmltox
            return Add(new Conversion(global));
        }

        public void AddObject(IntPtr converter, IntPtr objectSettings, byte[] data)
        {
            var settings = Get<SettingBag>(objectSettings);
            Remove(objectSettings);
            Get<Conversion>(converter).Objects.Add((settings, data));
        }

        public void AddObject(IntPtr converter, IntPtr objectSettings, string data)
        {
            AddObject(converter, objectSettings, data == null ? null : Encoding.UTF8.GetBytes(data));
        }

        public bool DoConversion(IntPtr converter) => Get<Conversion>(converter).Run(converter);

        public void DestroyConverter(IntPtr converter) => Remove(converter);

        public byte[] GetConversionResult(IntPtr converter) => Get<Conversion>(converter).Result;

        public int SetPhaseChangedCallback(IntPtr converter, VoidCallback callback)
        {
            Get<Conversion>(converter).PhaseChanged = callback;
            return 1;
        }

        public int SetProgressChangedCallback(IntPtr converter, VoidCallback callback)
        {
            Get<Conversion>(converter).ProgressChanged = callback;
            return 1;
        }

        public int SetFinishedCallback(IntPtr converter, IntCallback callback)
        {
            Get<Conversion>(converter).Finished = callback;
            return 1;
        }

        public int SetWarningCallback(IntPtr converter, StringCallback callback)
        {
            Get<Conversion>(converter).Warning = callback;
            return 1;
        }

        public int SetErrorCallback(IntPtr converter, StringCallback callback)
        {
            Get<Conversion>(converter).Error = callback;
            return 1;
        }

        public int GetPhaseCount(IntPtr converter) => Conversion.PhaseCount;

        public int GetCurrentPhase(IntPtr converter) => Get<Conversion>(converter).CurrentPhase;

        public string GetPhaseDescription(IntPtr converter, int phase) => Conversion.PhaseDescription(phase);

        public string GetProgressString(IntPtr converter) => Get<Conversion>(converter).ProgressString;

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
