namespace DinkToPdf
{
    /// <summary>
    /// This enum "extends" UnmanagedType enum from System.Runtime.InteropServices v4.1.0 which doesn't have LPUTF8Str (enum value is 48) enumartion defined
    /// </summary>
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)] // kept for code that names it; not offered to new code
    public enum CustomUnmanagedType
    {
        /// <summary>A string passed as UTF-8 (UnmanagedType.LPUTF8Str).</summary>
        LPUTF8Str = 48
    }
}
