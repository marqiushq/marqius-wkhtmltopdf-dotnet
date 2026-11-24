using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DinkToPdf.EventDefinitions;

namespace DinkToPdf.Contracts
{
    /// <summary>A converter from HTML to PDF: what ASP.NET Core gives a controller when the app registers <c>SynchronizedConverter</c>.</summary>
    public interface IConverter
    {

        /// <summary>
        ///  Converts document based on given settings
        /// </summary>
        /// <param name="document">Document to convert</param>
        /// <returns>Returns converted document in bytes</returns>
        byte[] Convert(IDocument document);

        /// <summary>Raised when a conversion moves to its next phase.</summary>
        event EventHandler<PhaseChangedArgs> PhaseChanged;

        /// <summary>Raised as a conversion goes on.</summary>
        event EventHandler<ProgressChangedArgs> ProgressChanged;

        /// <summary>Raised when a conversion ends.</summary>
        event EventHandler<FinishedArgs> Finished;

        /// <summary>Raised when something stops a PDF from being made.</summary>
        event EventHandler<ErrorArgs> Error;

        /// <summary>Raised for something worth knowing that did not stop the PDF.</summary>
        event EventHandler<WarningArgs> Warning;

    }
}
