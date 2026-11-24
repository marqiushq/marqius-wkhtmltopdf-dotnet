using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DinkToPdf.Contracts;

namespace DinkToPdf.EventDefinitions
{
    /// <summary>How far a conversion has got.</summary>
    public class ProgressChangedArgs : EventArgs
    {
        /// <summary>The document being converted.</summary>
        public IDocument Document { get; set; }

        /// <summary>How far, such as "Page 1 of 3".</summary>
        public string Description { get; set; }
    }
}
