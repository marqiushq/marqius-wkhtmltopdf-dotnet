using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace DinkToPdf
{
    /// <summary>
    /// DinkToPdf's calls into libwkhtmltox, with the same names and signatures, for code that makes them
    /// itself. They work through marqius-wkhtmltopdf, as <see cref="PdfTools"/> does: no native library is
    /// loaded. Text and output given back as pointers stay valid until the converter is destroyed (the
    /// version's, for as long as the app runs).
    /// </summary>
    public unsafe static class WkHtmlToXBindings
    {
        private static readonly PdfTools Tools = new PdfTools();

        // memory given out as libwkhtmltox's pointers, by converter: freed with the converter
        private static readonly ConcurrentDictionary<long, List<IntPtr>> Given = new ConcurrentDictionary<long, List<IntPtr>>();

        private static IntPtr version;

        /// <summary>1: marqius-wkhtmltopdf has what wkhtmltopdf's patched Qt gave it (headers, footers, outlines, links).</summary>
        public static int wkhtmltopdf_extended_qt() => 1;

        /// <summary>The wkhtmltopdf version marqius-wkhtmltopdf works as ("0.12.6"), as UTF-8 text.</summary>
        public static IntPtr wkhtmltopdf_version()
        {
            if (version == IntPtr.Zero)
            {
                var text = Copy(Tools.GetLibraryVersion());
                if (Interlocked.CompareExchange(ref version, text, IntPtr.Zero) != IntPtr.Zero)
                    Marshal.FreeHGlobal(text);
            }
            return version;
        }

        /// <summary>Finds marqius-wkhtmltopdf for this computer: 1 when it is there.</summary>
        /// <param name="useGraphics">Not used: marqius-wkhtmltopdf needs no graphics system.</param>
        public static int wkhtmltopdf_init(int useGraphics)
        {
            Tools.Load();
            return 1;
        }

        /// <summary>1: there is nothing to unload.</summary>
        public static int wkhtmltopdf_deinit() => 1;

        /// <summary>New global settings, to set and then give to <see cref="wkhtmltopdf_create_converter"/>.</summary>
        public static IntPtr wkhtmltopdf_create_global_settings() => Tools.CreateGlobalSettings();

        /// <summary>Sets a global setting by its libwkhtmltox name: 1 when that setting exists, 0 when not.</summary>
        /// <param name="settings">The global settings.</param>
        /// <param name="name">The setting's name, such as "size.paperSize".</param>
        /// <param name="value">Its value, such as "A4".</param>
        public static int wkhtmltopdf_set_global_setting(IntPtr settings, string name, string value) => Tools.SetGlobalSetting(settings, name, value);

        /// <summary>Copies a global setting's value into a buffer, as UTF-8 ending with a zero byte: 1 when that setting exists, 0 when not.</summary>
        /// <param name="settings">The global settings.</param>
        /// <param name="name">The setting's name.</param>
        /// <param name="value">The buffer.</param>
        /// <param name="valueSize">The buffer's size in bytes.</param>
        public static int wkhtmltopdf_get_global_setting(IntPtr settings, string name, byte* value, int valueSize) =>
            Copy(PdfTools.KnowsSetting(settings, name), Tools.GetGlobalSetting(settings, name), value, valueSize);

        /// <summary>Throws away global settings that were not given to a converter: 1.</summary>
        /// <param name="settings">The global settings.</param>
        public static int wkhtmltopdf_destroy_global_settings(IntPtr settings)
        {
            Tools.DestroyGlobalSetting(settings);
            return 1;
        }

        /// <summary>New settings for one object (a page or a table of contents), to set and then give to <see cref="wkhtmltopdf_add_object(IntPtr, IntPtr, string)"/>.</summary>
        public static IntPtr wkhtmltopdf_create_object_settings() => Tools.CreateObjectSettings();

        /// <summary>Sets an object's setting by its libwkhtmltox name: 1 when that setting exists, 0 when not.</summary>
        /// <param name="settings">The object's settings.</param>
        /// <param name="name">The setting's name, such as "web.printMediaType".</param>
        /// <param name="value">Its value, such as "true".</param>
        public static int wkhtmltopdf_set_object_setting(IntPtr settings, string name, string value) => Tools.SetObjectSetting(settings, name, value);

        /// <summary>Copies an object's setting into a buffer, as UTF-8 ending with a zero byte: 1 when that setting exists, 0 when not.</summary>
        /// <param name="settings">The object's settings.</param>
        /// <param name="name">The setting's name.</param>
        /// <param name="value">The buffer.</param>
        /// <param name="valueSize">The buffer's size in bytes.</param>
        public static int wkhtmltopdf_get_object_setting(IntPtr settings, string name, byte* value, int valueSize) =>
            Copy(PdfTools.KnowsSetting(settings, name), Tools.GetObjectSetting(settings, name), value, valueSize);

        /// <summary>Throws away an object's settings that were not added to a converter: 1.</summary>
        /// <param name="settings">The object's settings.</param>
        public static int wkhtmltopdf_destroy_object_settings(IntPtr settings)
        {
            Tools.DestroyObjectSetting(settings);
            return 1;
        }

        /// <summary>A converter, which owns these global settings from now on.</summary>
        /// <param name="globalSettings">The global settings.</param>
        public static IntPtr wkhtmltopdf_create_converter(IntPtr globalSettings) => Tools.CreateConverter(globalSettings);

        /// <summary>Adds an object to the converter, which owns its settings from now on: its HTML as UTF-8 bytes, or null for the page its "page" setting names.</summary>
        /// <param name="converter">The converter.</param>
        /// <param name="objectSettings">The object's settings.</param>
        /// <param name="data">The HTML, as UTF-8 bytes, or null.</param>
        public static void wkhtmltopdf_add_object(IntPtr converter, IntPtr objectSettings, byte[] data) => Tools.AddObject(converter, objectSettings, data);

        /// <summary>Adds an object to the converter, which owns its settings from now on: its HTML, or null for the page its "page" setting names.</summary>
        /// <param name="converter">The converter.</param>
        /// <param name="objectSettings">The object's settings.</param>
        /// <param name="data">The HTML, or null.</param>
        public static void wkhtmltopdf_add_object(IntPtr converter, IntPtr objectSettings, string data) => Tools.AddObject(converter, objectSettings, data);

        /// <summary>Makes the PDF: true when it was made. The callbacks are called while it is made.</summary>
        /// <param name="converter">The converter.</param>
        public static bool wkhtmltopdf_convert(IntPtr converter) => Tools.DoConversion(converter);

        /// <summary>Throws away the converter, its settings, and the memory its pointers point at.</summary>
        /// <param name="converter">The converter.</param>
        public static void wkhtmltopdf_destroy_converter(IntPtr converter)
        {
            Tools.DestroyConverter(converter);
            if (Given.TryRemove(converter.ToInt64(), out var given))
            {
                lock (given)
                {
                    foreach (var p in given)
                        Marshal.FreeHGlobal(p);
                    given.Clear();
                }
            }
        }

        /// <summary>The PDF's length in bytes; <paramref name="data"/> points at its bytes until the converter is destroyed.</summary>
        /// <param name="converter">The converter.</param>
        /// <param name="data">Set to the PDF's bytes.</param>
        public static int wkhtmltopdf_get_output(IntPtr converter, out IntPtr data)
        {
            var bytes = Tools.GetConversionResult(converter);
            data = Keep(converter, Copy(bytes, terminate: false));
            return bytes.Length;
        }

        /// <summary>Sets the callback called when the conversion moves to its next phase: 1.</summary>
        /// <param name="converter">The converter.</param>
        /// <param name="callback">The callback.</param>
        public static int wkhtmltopdf_set_phase_changed_callback(IntPtr converter, VoidCallback callback) => Tools.SetPhaseChangedCallback(converter, callback);

        /// <summary>Sets the callback called as the conversion goes on: 1.</summary>
        /// <param name="converter">The converter.</param>
        /// <param name="callback">The callback.</param>
        public static int wkhtmltopdf_set_progress_changed_callback(IntPtr converter, VoidCallback callback) => Tools.SetProgressChangedCallback(converter, callback);

        /// <summary>Sets the callback called when the conversion ends, with 1 when the PDF was made: 1.</summary>
        /// <param name="converter">The converter.</param>
        /// <param name="callback">The callback.</param>
        public static int wkhtmltopdf_set_finished_callback(IntPtr converter, IntCallback callback) => Tools.SetFinishedCallback(converter, callback);

        /// <summary>Sets the callback called with each warning: 1.</summary>
        /// <param name="converter">The converter.</param>
        /// <param name="callback">The callback.</param>
        public static int wkhtmltopdf_set_warning_callback(IntPtr converter, StringCallback callback) => Tools.SetWarningCallback(converter, callback);

        /// <summary>Sets the callback called with each error: 1.</summary>
        /// <param name="converter">The converter.</param>
        /// <param name="callback">The callback.</param>
        public static int wkhtmltopdf_set_error_callback(IntPtr converter, StringCallback callback) => Tools.SetErrorCallback(converter, callback);

        /// <summary>How many phases a conversion has.</summary>
        /// <param name="converter">The converter.</param>
        public static int wkhtmltopdf_phase_count(IntPtr converter) => Tools.GetPhaseCount(converter);

        /// <summary>The phase the conversion is in, from 0.</summary>
        /// <param name="converter">The converter.</param>
        public static int wkhtmltopdf_current_phase(IntPtr converter) => Tools.GetCurrentPhase(converter);

        /// <summary>A phase's name, such as "Loading pages", as UTF-8 text valid until the converter is destroyed.</summary>
        /// <param name="converter">The converter.</param>
        /// <param name="phase">The phase, from 0.</param>
        public static IntPtr wkhtmltopdf_phase_description(IntPtr converter, int phase) => Keep(converter, Copy(Tools.GetPhaseDescription(converter, phase)));

        /// <summary>The conversion's progress, such as "Page 1 of 3", as UTF-8 text valid until the converter is destroyed.</summary>
        /// <param name="converter">The converter.</param>
        public static IntPtr wkhtmltopdf_progress_string(IntPtr converter) => Keep(converter, Copy(Tools.GetProgressString(converter)));

        /// <summary>The HTTP status that stopped a page (404, for example), or 0.</summary>
        /// <param name="converter">The converter.</param>
        public static int wkhtmltopdf_http_error_code(IntPtr converter) => PdfTools.HttpErrorCode(converter);

        private static int Copy(bool known, string text, byte* value, int valueSize)
        {
            if (value != null && valueSize > 0)
            {
                var bytes = Encoding.UTF8.GetBytes(text ?? "");
                var n = Math.Min(bytes.Length, valueSize - 1);
                for (int i = 0; i < n; i++)
                    value[i] = bytes[i];
                value[n] = 0;
            }
            return known ? 1 : 0;
        }

        private static IntPtr Copy(string text) => Copy(Encoding.UTF8.GetBytes(text ?? ""), terminate: true);

        private static IntPtr Copy(byte[] bytes, bool terminate)
        {
            // never a null pointer, even for no bytes: callers copy from it
            var p = Marshal.AllocHGlobal(Math.Max(1, bytes.Length + (terminate ? 1 : 0)));
            Marshal.Copy(bytes, 0, p, bytes.Length);
            if (terminate)
                Marshal.WriteByte(p, bytes.Length, 0);
            return p;
        }

        private static IntPtr Keep(IntPtr converter, IntPtr p)
        {
            var given = Given.GetOrAdd(converter.ToInt64(), _ => new List<IntPtr>());
            lock (given)
                given.Add(p);
            return p;
        }
    }
}
