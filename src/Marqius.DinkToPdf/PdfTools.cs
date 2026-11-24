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

        /// <summary>
        /// The wkhtmltopdf version marqius-wkhtmltopdf works as: "0.12.6", a plain version number as
        /// libwkhtmltox gave it (DinkToPdf's own gave "0.12.4"). marqius-wkhtmltopdf's own version is
        /// <c>Marqius.Wkhtmltopdf.Wkhtmltopdf.Version</c>.
        /// </summary>
        public string GetLibraryVersion() => "0.12.6";

        /// <summary>New settings for the whole PDF, to fill in with <see cref="SetGlobalSetting"/> and then give to <see cref="CreateConverter"/>. Returns a handle to them.</summary>
        public IntPtr CreateGlobalSettings() => Add(new SettingBag(global: true));

        /// <summary>Sets one setting for the whole PDF by its libwkhtmltox name. Returns 1 when the name is known, 0 when not.</summary>
        /// <param name="settings">The settings for the whole PDF, from <see cref="CreateGlobalSettings"/>.</param>
        /// <param name="name">The setting's libwkhtmltox name, such as "orientation" or "size.paperSize".</param>
        /// <param name="value">Its value as text, such as "Landscape" or "A4".</param>
        public int SetGlobalSetting(IntPtr settings, string name, string value) => Get<SettingBag>(settings).Set(name, value) ? 1 : 0;

        /// <summary>One setting for the whole PDF, by its libwkhtmltox name: its value as text, or empty when it was not set.</summary>
        /// <param name="settings">The settings for the whole PDF, from <see cref="CreateGlobalSettings"/>.</param>
        /// <param name="name">The setting's libwkhtmltox name, such as "orientation".</param>
        public string GetGlobalSetting(IntPtr settings, string name) => Get<SettingBag>(settings).Get(name);

        /// <summary>Throws away settings for the whole PDF that were never given to a converter.</summary>
        /// <param name="settings">The settings for the whole PDF, from <see cref="CreateGlobalSettings"/>.</param>
        public void DestroyGlobalSetting(IntPtr settings) => Remove(settings);

        /// <summary>New settings for one page of the PDF, to fill in with <see cref="SetObjectSetting"/> and then add with <see cref="AddObject(IntPtr, IntPtr, string)"/>. Returns a handle to them.</summary>
        public IntPtr CreateObjectSettings() => Add(new SettingBag(global: false));

        /// <summary>Sets one setting of a page by its libwkhtmltox name. Returns 1 when the name is known, 0 when not.</summary>
        /// <param name="settings">The page's settings, from <see cref="CreateObjectSettings"/>.</param>
        /// <param name="name">The setting's libwkhtmltox name, such as "web.loadImages".</param>
        /// <param name="value">Its value as text, such as "true".</param>
        public int SetObjectSetting(IntPtr settings, string name, string value) => Get<SettingBag>(settings).Set(name, value) ? 1 : 0;

        /// <summary>One setting of a page, by its libwkhtmltox name: its value as text, or empty when it was not set.</summary>
        /// <param name="settings">The page's settings, from <see cref="CreateObjectSettings"/>.</param>
        /// <param name="name">The setting's libwkhtmltox name, such as "web.loadImages".</param>
        public string GetObjectSetting(IntPtr settings, string name) => Get<SettingBag>(settings).Get(name);

        /// <summary>Throws away a page's settings that were never added to a converter.</summary>
        /// <param name="settings">The page's settings, from <see cref="CreateObjectSettings"/>.</param>
        public void DestroyObjectSetting(IntPtr settings) => Remove(settings);

        /// <summary>A new conversion with these settings for the whole PDF, which it owns from now on. Returns a handle to it.</summary>
        /// <param name="globalSettings">The settings for the whole PDF, from <see cref="CreateGlobalSettings"/>.</param>
        public IntPtr CreateConverter(IntPtr globalSettings)
        {
            var global = Get<SettingBag>(globalSettings);
            Remove(globalSettings); // the converter owns them now, as in libwkhtmltox
            return Add(new Conversion(global));
        }

        /// <summary>Adds a page to the conversion, which owns the page's settings from now on.</summary>
        /// <param name="converter">The conversion, from <see cref="CreateConverter"/>.</param>
        /// <param name="objectSettings">The page's settings, from <see cref="CreateObjectSettings"/>.</param>
        /// <param name="data">The page's HTML as UTF-8 bytes, or null when its "page" setting names the page to load.</param>
        public void AddObject(IntPtr converter, IntPtr objectSettings, byte[] data)
        {
            var settings = Get<SettingBag>(objectSettings);
            Remove(objectSettings);
            Get<Conversion>(converter).Objects.Add((settings, data));
        }

        /// <summary>Adds a page to the conversion, which owns the page's settings from now on.</summary>
        /// <param name="converter">The conversion, from <see cref="CreateConverter"/>.</param>
        /// <param name="objectSettings">The page's settings, from <see cref="CreateObjectSettings"/>.</param>
        /// <param name="data">The page's HTML, or null when its "page" setting names the page to load.</param>
        public void AddObject(IntPtr converter, IntPtr objectSettings, string data)
        {
            AddObject(converter, objectSettings, data == null ? null : Encoding.UTF8.GetBytes(data));
        }

        /// <summary>Makes the PDF, calling the callbacks as it goes. Returns true when the PDF was made.</summary>
        /// <param name="converter">The conversion, from <see cref="CreateConverter"/>.</param>
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

        /// <summary>Throws away the conversion and everything it holds.</summary>
        /// <param name="converter">The conversion, from <see cref="CreateConverter"/>.</param>
        public void DestroyConverter(IntPtr converter) => Remove(converter);

        /// <summary>The PDF the conversion made, as bytes: empty when it was not made, or when it was written to the file named in "out".</summary>
        /// <param name="converter">The conversion, from <see cref="CreateConverter"/>.</param>
        public byte[] GetConversionResult(IntPtr converter) => Get<Conversion>(converter).Result;

        /// <summary>Sets the method called when the conversion moves to its next phase, such as "Printing pages". Returns 1.</summary>
        /// <param name="converter">The conversion, from <see cref="CreateConverter"/>.</param>
        /// <param name="callback">The method to call.</param>
        public int SetPhaseChangedCallback(IntPtr converter, VoidCallback callback)
        {
            Get<Conversion>(converter).PhaseChanged = callback;
            return 1;
        }

        /// <summary>Sets the method called as the conversion goes on, such as at each page. Returns 1.</summary>
        /// <param name="converter">The conversion, from <see cref="CreateConverter"/>.</param>
        /// <param name="callback">The method to call.</param>
        public int SetProgressChangedCallback(IntPtr converter, VoidCallback callback)
        {
            Get<Conversion>(converter).ProgressChanged = callback;
            return 1;
        }

        /// <summary>Sets the method called when the conversion ends, with 1 when the PDF was made and 0 when not. Returns 1.</summary>
        /// <param name="converter">The conversion, from <see cref="CreateConverter"/>.</param>
        /// <param name="callback">The method to call.</param>
        public int SetFinishedCallback(IntPtr converter, IntCallback callback)
        {
            Get<Conversion>(converter).Finished = callback;
            return 1;
        }

        /// <summary>Sets the method called with each warning, such as a picture that could not be loaded. Returns 1.</summary>
        /// <param name="converter">The conversion, from <see cref="CreateConverter"/>.</param>
        /// <param name="callback">The method to call.</param>
        public int SetWarningCallback(IntPtr converter, StringCallback callback)
        {
            Get<Conversion>(converter).Warning = callback;
            return 1;
        }

        /// <summary>Sets the method called with each error, such as a page that could not be loaded. Returns 1.</summary>
        /// <param name="converter">The conversion, from <see cref="CreateConverter"/>.</param>
        /// <param name="callback">The method to call.</param>
        public int SetErrorCallback(IntPtr converter, StringCallback callback)
        {
            Get<Conversion>(converter).Error = callback;
            return 1;
        }

        /// <summary>How many phases a conversion has: 6.</summary>
        /// <param name="converter">The conversion, from <see cref="CreateConverter"/>.</param>
        public int GetPhaseCount(IntPtr converter) => Conversion.PhaseCount;

        /// <summary>The phase the conversion is in, counted from 0.</summary>
        /// <param name="converter">The conversion, from <see cref="CreateConverter"/>.</param>
        public int GetCurrentPhase(IntPtr converter) => Get<Conversion>(converter).CurrentPhase;

        /// <summary>A phase's name, such as "Loading pages" or "Printing pages".</summary>
        /// <param name="converter">The conversion, from <see cref="CreateConverter"/>.</param>
        /// <param name="phase">The phase, counted from 0.</param>
        public string GetPhaseDescription(IntPtr converter, int phase) => Conversion.PhaseDescription(phase);

        /// <summary>How far the conversion has got, such as "Page 1 of 3".</summary>
        /// <param name="converter">The conversion, from <see cref="CreateConverter"/>.</param>
        public string GetProgressString(IntPtr converter) => Get<Conversion>(converter).ProgressString;

        // as in DinkToPdf, Dispose is only reached through IDisposable (using, or the DI container)
        void IDisposable.Dispose()
        {
            // nothing to unload: there is no native library
        }

        // for WkHtmlToXBindings: libwkhtmltox's answers that ITools does not give
        internal static bool KnowsSetting(IntPtr settings, string name) => Get<SettingBag>(settings).Knows(name);

        internal static int HttpErrorCode(IntPtr converter) => Get<Conversion>(converter).HttpErrorCode;

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
