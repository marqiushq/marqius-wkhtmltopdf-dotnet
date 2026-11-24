using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DinkToPdf.Contracts;

namespace DinkToPdf
{
    /// <summary>What happens when a page cannot be loaded.</summary>
    public enum ContentErrorHandling
    {
        /// <summary>Stop: no PDF is made (the default).</summary>
        Abort,
        /// <summary>Leave the page out, and make the PDF of the rest.</summary>
        Skip,
        /// <summary>Make the PDF with whatever could be loaded.</summary>
        Ignore
    }

    /// <summary>How a page is loaded: sign-in, cookies, headers, scripts' time, zoom, files on this computer.</summary>
    public class LoadSettings : ISettings
    {
        /// <summary>How a page is loaded (cookies, headers, load errors, zoom), none set yet: each one left unset keeps its default.</summary>
        public LoadSettings()
        {
        }

        /// <summary>
        /// The user name to use when loging into a website. Default = ""
        /// </summary>
        [WkHtml("load.username")]
        public string Username { get; set; }

        /// <summary>
        /// The password to used when logging into a website. Default = ""
        /// </summary>
        [WkHtml("load.password")]
        public string Password { get; set; }

        /// <summary>
        /// The mount of time in milliseconds to wait after a page has done loading until it is actually printed. E.g. "1200". We will wait this amount of time or until, javascript calls window.print(). Default = 200
        /// </summary>
        [WkHtml("load.jsdelay")]
        public int? JSDelay { get; set; }

        /// <summary>
        /// How much should we zoom in on the content. Default = 1.0
        /// </summary>
        [WkHtml("load.zoomFactor")]
        public double? ZoomFactor { get; set; }

        /// <summary>
        /// Disallow local and piped files to access other local files. Default = false
        /// </summary>
        [WkHtml("load.blockLocalFileAccess")]
        public bool? BlockLocalFileAccess { get; set; }

        /// <summary>
        /// Stop slow running javascript. Default = true
        /// </summary>
        [WkHtml("load.stopSlowScript")]
        public bool? StopSlowScript { get; set; }

        /// <summary>
        /// Forward javascript warnings and errors to the warning callback. Default = false
        /// </summary>
        [WkHtml("load.debugJavascript")]
        public bool? DebugJavascript { get; set; }

        /// <summary>
        /// How should we handle obejcts that fail to load. Default = Abort
        /// </summary>
        [WkHtml("load.loadErrorHandling")]
        public ContentErrorHandling? LoadErrorHandling { get; set; }

        /// <summary>
        /// String describing what proxy to use when loading the object. Default = ""
        /// </summary>
        [WkHtml("load.proxy")]
        public string Proxy { get; set; }

        /// <summary>Extra HTTP headers sent when the page is requested, by name and value, such as an Authorization header (with <see cref="RepeatCustomHeaders"/> = true, also for its pictures and style sheets). Default = empty</summary>
        [WkHtml("load.customHeaders")]
        public Dictionary<string, string> CustomHeaders { get; set; }

        /// <summary>
        /// Should the custom headers be sent all elements loaded instead of only the main page. Default = false
        /// </summary>
        [WkHtml("load.repeatCustomHeaders")]
        public bool? RepeatCustomHeaders { get; set; }

        /// <summary>Cookies sent when the page is requested, by name and value. Default = empty</summary>
        [WkHtml("load.cookies")]
        public Dictionary<string, string> Cookies { get; set; }
    }
}
