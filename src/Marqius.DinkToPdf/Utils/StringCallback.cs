using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace DinkToPdf
{
    /// <summary>A callback with a message, such as a warning or an error.</summary>
    /// <param name="converter">The conversion it is about.</param>
    /// <param name="str">The message.</param>
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void StringCallback(IntPtr converter, [MarshalAs(UnmanagedType.LPStr)] string str);
}
