using DinkToPdf.Contracts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DinkToPdf
{
    /// <summary>One page of HTML in the PDF: HTML you give (<see cref="HtmlContent"/>) or a web address or file (<see cref="Page"/>), with how it looks, its header and footer, and how it loads.</summary>
    public class ObjectSettings : IObject
    {
        /// <summary>One page of the PDF: set <see cref="HtmlContent"/> to your HTML, or <see cref="Page"/> to a web address or file.</summary>
        public ObjectSettings()
        {
        }

        /// <summary>
        /// The URL or path of the web page to convert, if "-" input is read from stdin. Default = ""
        /// </summary>
        [WkHtml("page")]
        public string Page { get; set; }

        /// <summary>
        /// Should external links in the HTML document be converted into external pdf links. Default = true
        /// </summary>
        [WkHtml("useExternalLinks")]
        public bool? UseExternalLinks { get; set; }

        /// <summary>
        /// Should internal links in the HTML document be converted into pdf references. Default = true
        /// </summary>
        [WkHtml("useLocalLinks")]
        public bool? UseLocalLinks { get; set; }

        /// <summary>
        /// Should we turn HTML forms into PDF forms. Default = false
        /// </summary>
        [WkHtml("produceForms")]
        public bool? ProduceForms { get; set; }

        /// <summary>
        /// Should the sections from this document be included in the outline and table of content. Default = false
        /// </summary>
        [WkHtml("includeInOutline")]
        public bool? IncludeInOutline { get; set; }

        /// <summary>
        /// Should we count the pages of this document, in the counter used for TOC, headers and footers. Default = false
        /// </summary>
        [WkHtml("pagesCount")]
        public bool? PagesCount { get; set; }

        /// <summary>The HTML of this page, as a string. Pictures and stylesheets it names by full address load; files on this computer load as <c>file:///</c> addresses.</summary>
        public string HtmlContent { get; set; }

        private WebSettings webSettings = new WebSettings();

        /// <summary>How this page looks: backgrounds, pictures, scripts, print styles.</summary>
        public WebSettings WebSettings {
            get { return webSettings; }
            set { webSettings = value; }
        }

        private HeaderSettings headerSettings = new HeaderSettings();

        /// <summary>The text or HTML at the top of each page.</summary>
        public HeaderSettings HeaderSettings {
            get { return headerSettings; }
            set { headerSettings = value; }
        }

        private FooterSettings footerSettings = new FooterSettings();

        /// <summary>The text or HTML at the foot of each page.</summary>
        public FooterSettings FooterSettings {
            get { return footerSettings; }
            set { footerSettings = value; }
        }

        private LoadSettings loadSettings = new LoadSettings();

        /// <summary>How this page is loaded: sign-in, cookies, headers, scripts' time, zoom.</summary>
        public LoadSettings LoadSettings {
            get { return loadSettings; }
            set { loadSettings = value; }
        }

        /// <summary><see cref="HtmlContent"/> as UTF-8 bytes (empty when there is none).</summary>
        public byte[] GetContent()
        {
            if (HtmlContent == null)
            {
                return new byte[0];
            }

            return Encoding.UTF8.GetBytes(HtmlContent);
        }
    }
}
