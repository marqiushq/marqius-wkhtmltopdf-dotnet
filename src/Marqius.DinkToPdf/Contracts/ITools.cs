using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DinkToPdf.Contracts
{
    /// <summary>The calls libwkhtmltox had, which <see cref="PdfTools"/> answers with marqius-wkhtmltopdf. Apps rarely call them: the converters do.</summary>
    public interface ITools : IDisposable
    {
        /// <summary>Gets the tools ready: with <see cref="PdfTools"/>, finds marqius-wkhtmltopdf. The converters call it before their first PDF.</summary>
        void Load();

        /// <summary>True once <see cref="Load"/> has got the tools ready.</summary>
        bool IsLoaded { get; }

        /// <summary>True when headers, footers, outlines and links are available: always, with marqius-wkhtmltopdf.</summary>
        bool ExtendedQt();

        /// <summary>The wkhtmltopdf version, as a plain version number such as "0.12.6".</summary>
        string GetLibraryVersion();

        /// <summary>New settings for the whole PDF, to fill in with <see cref="SetGlobalSetting"/> and then give to <see cref="CreateConverter"/>. Returns a handle to them.</summary>
        IntPtr CreateGlobalSettings();

        /// <summary>Sets one setting for the whole PDF by its libwkhtmltox name. Returns 1 when the name is known, 0 when not.</summary>
        /// <param name="settings">The settings for the whole PDF, from <see cref="CreateGlobalSettings"/>.</param>
        /// <param name="name">The setting's libwkhtmltox name, such as "orientation" or "size.paperSize".</param>
        /// <param name="value">Its value as text, such as "Landscape" or "A4".</param>
        int SetGlobalSetting(IntPtr settings, string name, string value);

        /// <summary>One setting for the whole PDF, by its libwkhtmltox name: its value as text, or empty when it was not set.</summary>
        /// <param name="settings">The settings for the whole PDF, from <see cref="CreateGlobalSettings"/>.</param>
        /// <param name="name">The setting's libwkhtmltox name, such as "orientation".</param>
        string GetGlobalSetting(IntPtr settings, string name);

        /// <summary>Throws away settings for the whole PDF that were never given to a converter.</summary>
        /// <param name="settings">The settings for the whole PDF, from <see cref="CreateGlobalSettings"/>.</param>
        void DestroyGlobalSetting(IntPtr settings);

        /// <summary>New settings for one page of the PDF, to fill in with <see cref="SetObjectSetting"/> and then add with <see cref="AddObject(IntPtr, IntPtr, string)"/>. Returns a handle to them.</summary>
        IntPtr CreateObjectSettings();

        /// <summary>Sets one setting of a page by its libwkhtmltox name. Returns 1 when the name is known, 0 when not.</summary>
        /// <param name="settings">The page's settings, from <see cref="CreateObjectSettings"/>.</param>
        /// <param name="name">The setting's libwkhtmltox name, such as "web.loadImages".</param>
        /// <param name="value">Its value as text, such as "true".</param>
        int SetObjectSetting(IntPtr settings, string name, string value);

        /// <summary>One setting of a page, by its libwkhtmltox name: its value as text, or empty when it was not set.</summary>
        /// <param name="settings">The page's settings, from <see cref="CreateObjectSettings"/>.</param>
        /// <param name="name">The setting's libwkhtmltox name, such as "web.loadImages".</param>
        string GetObjectSetting(IntPtr settings, string name);

        /// <summary>Throws away a page's settings that were never added to a converter.</summary>
        /// <param name="settings">The page's settings, from <see cref="CreateObjectSettings"/>.</param>
        void DestroyObjectSetting(IntPtr settings);

        /// <summary>A new conversion with these settings for the whole PDF, which it owns from now on. Returns a handle to it.</summary>
        /// <param name="globalSettings">The settings for the whole PDF, from <see cref="CreateGlobalSettings"/>.</param>
        IntPtr CreateConverter(IntPtr globalSettings);

        /// <summary>Adds a page to the conversion, which owns the page's settings from now on.</summary>
        /// <param name="converter">The conversion, from <see cref="CreateConverter"/>.</param>
        /// <param name="objectSettings">The page's settings, from <see cref="CreateObjectSettings"/>.</param>
        /// <param name="data">The page's HTML as UTF-8 bytes, or null when its "page" setting names the page to load.</param>
        void AddObject(IntPtr converter, IntPtr objectSettings, byte[] data);

        /// <summary>Adds a page to the conversion, which owns the page's settings from now on.</summary>
        /// <param name="converter">The conversion, from <see cref="CreateConverter"/>.</param>
        /// <param name="objectSettings">The page's settings, from <see cref="CreateObjectSettings"/>.</param>
        /// <param name="data">The page's HTML, or null when its "page" setting names the page to load.</param>
        void AddObject(IntPtr converter, IntPtr objectSettings, string data);

        /// <summary>Makes the PDF, calling the callbacks as it goes. Returns true when the PDF was made.</summary>
        /// <param name="converter">The conversion, from <see cref="CreateConverter"/>.</param>
        bool DoConversion(IntPtr converter);

        /// <summary>Throws away the conversion and everything it holds.</summary>
        /// <param name="converter">The conversion, from <see cref="CreateConverter"/>.</param>
        void DestroyConverter(IntPtr converter);

        /// <summary>The PDF the conversion made, as bytes: empty when it was not made, or when it was written to the file named in "out".</summary>
        /// <param name="converter">The conversion, from <see cref="CreateConverter"/>.</param>
        byte[] GetConversionResult(IntPtr converter);

        /// <summary>Sets the method called when the conversion moves to its next phase, such as "Printing pages". Returns 1.</summary>
        /// <param name="converter">The conversion, from <see cref="CreateConverter"/>.</param>
        /// <param name="callback">The method to call.</param>
        int SetPhaseChangedCallback(IntPtr converter, VoidCallback callback);

        /// <summary>Sets the method called as the conversion goes on, such as at each page. Returns 1.</summary>
        /// <param name="converter">The conversion, from <see cref="CreateConverter"/>.</param>
        /// <param name="callback">The method to call.</param>
        int SetProgressChangedCallback(IntPtr converter, VoidCallback callback);

        /// <summary>Sets the method called when the conversion ends, with 1 when the PDF was made and 0 when not. Returns 1.</summary>
        /// <param name="converter">The conversion, from <see cref="CreateConverter"/>.</param>
        /// <param name="callback">The method to call.</param>
        int SetFinishedCallback(IntPtr converter, IntCallback callback);

        /// <summary>Sets the method called with each warning, such as a picture that could not be loaded. Returns 1.</summary>
        /// <param name="converter">The conversion, from <see cref="CreateConverter"/>.</param>
        /// <param name="callback">The method to call.</param>
        int SetWarningCallback(IntPtr converter, StringCallback callback);

        /// <summary>Sets the method called with each error, such as a page that could not be loaded. Returns 1.</summary>
        /// <param name="converter">The conversion, from <see cref="CreateConverter"/>.</param>
        /// <param name="callback">The method to call.</param>
        int SetErrorCallback(IntPtr converter, StringCallback callback);

        /// <summary>How many phases a conversion has: 6.</summary>
        /// <param name="converter">The conversion, from <see cref="CreateConverter"/>.</param>
        int GetPhaseCount(IntPtr converter);

        /// <summary>The phase the conversion is in, counted from 0.</summary>
        /// <param name="converter">The conversion, from <see cref="CreateConverter"/>.</param>
        int GetCurrentPhase(IntPtr converter);

        /// <summary>A phase's name, such as "Loading pages" or "Printing pages".</summary>
        /// <param name="converter">The conversion, from <see cref="CreateConverter"/>.</param>
        /// <param name="phase">The phase, counted from 0.</param>
        string GetPhaseDescription(IntPtr converter, int phase);

        /// <summary>How far the conversion has got, such as "Page 1 of 3".</summary>
        /// <param name="converter">The conversion, from <see cref="CreateConverter"/>.</param>
        string GetProgressString(IntPtr converter);
    }
}
