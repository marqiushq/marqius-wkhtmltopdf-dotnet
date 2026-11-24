using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DinkToPdf
{
    /// <summary>Names the libwkhtmltox setting a property sets, such as "web.loadImages": a subclass of the settings may add its own.</summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public class WkHtmlAttribute : Attribute
    {
        /// <summary>The setting's libwkhtmltox name.</summary>
        public string Name { get; private set; }

        /// <summary>The setting this property sets, by its libwkhtmltox name.</summary>
        /// <param name="name">The setting's libwkhtmltox name, such as "web.loadImages".</param>
        public WkHtmlAttribute(string name)
        {
            Name = name;
        }
    }
}
