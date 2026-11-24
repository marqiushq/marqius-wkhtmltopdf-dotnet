using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DinkToPdf.Contracts;

namespace DinkToPdf.EventDefinitions
{
    /// <summary>What stopped a PDF from being made.</summary>
    public class ErrorArgs : EventArgs
    {
        /// <summary>The document being converted.</summary>
        public IDocument Document { get; set; }

        /// <summary>What went wrong, as the program says it.</summary>
        public string Message { get; set; }
    }
}
