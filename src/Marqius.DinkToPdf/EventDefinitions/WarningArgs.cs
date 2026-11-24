using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DinkToPdf.Contracts;

namespace DinkToPdf.EventDefinitions
{
    /// <summary>Something worth knowing that did not stop the PDF.</summary>
    public class WarningArgs : EventArgs
    {
        /// <summary>A warning, as the converter gives it to the Warning event.</summary>
        public WarningArgs()
        {
        }

        /// <summary>The document being converted.</summary>
        public IDocument Document { get; set; }

        /// <summary>The warning, as the program says it; a command to run follows on its own line where there is one.</summary>
        public string Message { get; set; }
    }
}
