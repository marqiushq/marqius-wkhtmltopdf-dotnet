using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DinkToPdf.Contracts
{
    /// <summary>The calls libwkhtmltox had, which <see cref="PdfTools"/> answers with marqius-wkhtmltopdf. Apps rarely call them: the converters do.</summary>
    public interface ITools : IDisposable
    {
        /// <summary>
        /// Load library and initialize wkhtmltopdf 
        /// </summary>
        void Load();

        /// <summary>
        /// Is set to true if library is loaded and wkhtmltopdf is initialized successfully
        /// </summary>
        bool IsLoaded { get; }

        /// <summary>True when headers, footers, outlines and links are available (always, with marqius-wkhtmltopdf).</summary>
        bool ExtendedQt();

        /// <summary>The version, such as "0.12.6 (marqius-wkhtmltopdf 0.12.14)".</summary>
        string GetLibraryVersion();

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        IntPtr CreateGlobalSettings();

        /// <summary>Sets a setting for the whole PDF by its libwkhtmltox name, such as "orientation": 1 when it is known, 0 when not.</summary>
        int SetGlobalSetting(IntPtr settings, string name, string value);

        /// <summary>A setting for the whole PDF, by its libwkhtmltox name.</summary>
        string GetGlobalSetting(IntPtr settings, string name);

        /// <summary>Lets go of settings no converter took.</summary>
        void DestroyGlobalSetting(IntPtr settings);

        /// <summary>Makes settings for one page.</summary>
        IntPtr CreateObjectSettings();

        /// <summary>Sets a page's setting by its libwkhtmltox name, such as "web.loadImages": 1 when it is known, 0 when not.</summary>
        int SetObjectSetting(IntPtr settings, string name, string value);

        /// <summary>A page's setting, by its libwkhtmltox name.</summary>
        string GetObjectSetting(IntPtr settings, string name);

        /// <summary>Lets go of page settings no converter took.</summary>
        void DestroyObjectSetting(IntPtr settings);

        /// <summary>Makes a conversion with these settings for the whole PDF.</summary>
        IntPtr CreateConverter(IntPtr globalSettings);

        /// <summary>Adds a page to a conversion: its settings, and its HTML as UTF-8 bytes (or none, when its settings name a page).</summary>
        void AddObject(IntPtr converter, IntPtr objectSettings, byte[] data);

        /// <summary>Adds a page to a conversion: its settings, and its HTML (or null, when its settings name a page).</summary>
        void AddObject(IntPtr converter, IntPtr objectSettings, string data);

        /// <summary>Makes the PDF: true when it was made.</summary>
        bool DoConversion(IntPtr converter);

        /// <summary>Lets go of a conversion and what it held.</summary>
        void DestroyConverter(IntPtr converter);

        /// <summary>The PDF a conversion made (empty when it was written to a file).</summary>
        byte[] GetConversionResult(IntPtr converter);

        /// <summary>Calls back at each phase.</summary>
        int SetPhaseChangedCallback(IntPtr converter, VoidCallback callback);

        /// <summary>Calls back as the conversion goes on.</summary>
        int SetProgressChangedCallback(IntPtr converter, VoidCallback callback);

        /// <summary>Calls back at the end, with 1 when the PDF was made.</summary>
        int SetFinishedCallback(IntPtr converter, IntCallback callback);

        /// <summary>Calls back with each warning.</summary>
        int SetWarningCallback(IntPtr converter, StringCallback callback);

        /// <summary>Calls back with each error.</summary>
        int SetErrorCallback(IntPtr converter, StringCallback callback);

        /// <summary>How many phases a conversion has (6).</summary>
        int GetPhaseCount(IntPtr converter);

        /// <summary>The phase now, from 0.</summary>
        int GetCurrentPhase(IntPtr converter);

        /// <summary>A phase's name, such as "Printing pages".</summary>
        string GetPhaseDescription(IntPtr converter, int phase);

        /// <summary>How far the conversion has got, such as "Page 1 of 3".</summary>
        string GetProgressString(IntPtr converter);
    }
}
