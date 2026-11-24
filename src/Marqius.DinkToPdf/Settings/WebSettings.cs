using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DinkToPdf.Contracts;

namespace DinkToPdf
{
    /// <summary>How a page looks: backgrounds, pictures, scripts, print styles, text encoding, smallest font size.</summary>
    public class WebSettings : ISettings
    {
        /// <summary>How a page is shown (pictures, scripts, print styles), none set yet: each one left unset keeps its default.</summary>
        public WebSettings()
        {
        }

        /// <summary>
        /// Should we print the background. Default = true
        /// </summary>
        [WkHtml("web.background")]
        public bool? Background { get; set; }

        /// <summary>
        /// Should we load images. Default = true
        /// </summary>
        [WkHtml("web.loadImages")]
        public bool? LoadImages { get; set; }

        /// <summary>
        /// Should we enable javascript. Default = true
        /// </summary>
        [WkHtml("web.enableJavascript")]
        public bool? EnableJavascript { get; set; }

        /// <summary>
        /// Should we enable intelligent shrinkng to fit more content on one page. Has no effect for wkhtmltoimage. Default = true
        /// </summary>
        [WkHtml("web.enableIntelligentShrinking")]
        public bool? EnableIntelligentShrinking { get; set; }

        /// <summary>The smallest font size allowed; -1 means no minimum. Default = -1</summary>
        [WkHtml("web.minimumFontSize")]
        public int? MinimumFontSize { get; set; }

        /// <summary>
        /// Should the content be printed using the print media type instead of the screen media type. Default = false
        /// </summary>
        [WkHtml("web.printMediaType")]
        public bool? PrintMediaType { get; set; }

        /// <summary>
        /// What encoding should we guess content is using if they do not specify it properly. Default = ""
        /// </summary>
        [WkHtml("web.defaultEncoding")]
        public string DefaultEncoding { get; set; }

        /// <summary>The web address or file path of an extra style sheet for the page, such as "print.css" (a relative path is from the app's current folder). Default = "" (none)</summary>
        [WkHtml("web.userStyleSheet")]
        public string UserStyleSheet { get; set; }

        /// <summary>
        /// Should we enable NS plugins. Enabling this will have limited success. Default = false
        /// </summary>
        [WkHtml("web.enablePlugins")]
        public bool? enablePlugins { get; set; }
    }
}
