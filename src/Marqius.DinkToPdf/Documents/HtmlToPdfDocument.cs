using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DinkToPdf.Contracts;

namespace DinkToPdf
{
    /// <summary>A PDF to make: settings for the whole PDF (<see cref="GlobalSettings"/>) and one or more objects, each a page of HTML (<see cref="Objects"/>).</summary>
    public class HtmlToPdfDocument : IDocument
    {
        /// <summary>The pages of HTML that make the PDF, in order: each is HTML you give, or a web address or file.</summary>
        public List<ObjectSettings> Objects { get;  private set;}
        
        private GlobalSettings globalSettings = new GlobalSettings();

        /// <summary>Settings for the whole PDF: paper size, orientation, margins, title.</summary>
        public GlobalSettings GlobalSettings {
            get { return globalSettings; }
            set {
                globalSettings = value;
            }
        }

        /// <summary>An empty document: add at least one object to <see cref="Objects"/>.</summary>
        public HtmlToPdfDocument()
        {
            Objects = new List<ObjectSettings>();
        }

        /// <summary>The document's objects, in order.</summary>
        public IEnumerable<IObject> GetObjects()
        {
            return Objects.ToArray();
        }   
    }
}
