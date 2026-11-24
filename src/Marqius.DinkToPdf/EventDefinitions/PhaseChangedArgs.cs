using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DinkToPdf.Contracts;

namespace DinkToPdf.EventDefinitions
{
    /// <summary>A conversion's next phase.</summary>
    public class PhaseChangedArgs : EventArgs
    {
        /// <summary>A new phase, as the converter gives it to the PhaseChanged event.</summary>
        public PhaseChangedArgs()
        {
        }

        /// <summary>The document being converted.</summary>
        public IDocument Document { get; set; }

        /// <summary>How many phases a conversion has (6).</summary>
        public int PhaseCount { get; set; }

        /// <summary>The phase now, from 0.</summary>
        public int CurrentPhase { get; set; }

        /// <summary>The phase, such as "Loading pages" or "Printing pages".</summary>
        public string Description { get; set; }
    }
}
