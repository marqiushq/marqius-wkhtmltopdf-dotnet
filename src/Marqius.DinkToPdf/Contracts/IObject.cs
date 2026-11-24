using System;
using System.Collections.Generic;
using System.Linq;

namespace DinkToPdf.Contracts
{
    /// <summary>One page of HTML in a document: <see cref="ObjectSettings"/>.</summary>
    public interface IObject : ISettings
    {
        /// <summary>The page's HTML as bytes, when it is given as HTML.</summary>
        byte[] GetContent();
    }
}
