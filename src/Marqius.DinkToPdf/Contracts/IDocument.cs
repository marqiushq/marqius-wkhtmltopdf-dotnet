using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DinkToPdf.Contracts
{
    /// <summary>A PDF to make: <see cref="HtmlToPdfDocument"/>.</summary>
    public interface IDocument : ISettings
    {
       /// <summary>The document's objects, in order.</summary>
       IEnumerable<IObject> GetObjects();
    }
}
