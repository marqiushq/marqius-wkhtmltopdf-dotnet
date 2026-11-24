using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DinkToPdf.Contracts;

namespace DinkToPdf.EventDefinitions
{
    /// <summary>A conversion's end.</summary>
    public class FinishedArgs : EventArgs
    {
        /// <summary>The end of a conversion, as the converter gives it to the Finished event.</summary>
        public FinishedArgs()
        {
        }

        /// <summary>The document that was converted.</summary>
        public IDocument Document { get; set; }

        /// <summary>True when the PDF was made.</summary>
        public bool Success { get; set; }
    }
}
