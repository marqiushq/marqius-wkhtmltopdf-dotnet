using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace DinkToPdf
{
    /// <summary>A callback with a number, such as the end of a conversion (1 when the PDF was made).</summary>
    /// <param name="converter">The conversion it is about.</param>
    /// <param name="integer">The number: at the end of a conversion, 1 when the PDF was made and 0 when not.</param>
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void IntCallback(IntPtr converter, int integer);
}
