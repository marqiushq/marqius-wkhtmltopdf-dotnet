using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Marqius.DinkToPdf.Tests
{
    /// <summary>
    /// Where the tests find the program: MARQIUS_WKHTMLTOPDF when it is set, else the one build.py laid
    /// out for this computer in builds/runtimes (run python3 build.py VERSION first).
    /// </summary>
    internal static class Setup
    {
        private static readonly object once = new object();
        private static bool done;

        internal static void Program()
        {
            lock (once)
            {
                if (done)
                    return;
                done = true;
                if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("MARQIUS_WKHTMLTOPDF")))
                    return;
                var rid = global::Marqius.Wkhtmltopdf.Wkhtmltopdf.RuntimeId;
                var name = rid != null && rid.StartsWith("win") ? "marqius-wkhtmltopdf.exe" : "marqius-wkhtmltopdf";
                for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
                {
                    var candidate = Path.Combine(dir.FullName, "builds", "runtimes", rid ?? "none", "native", name);
                    if (File.Exists(candidate))
                    {
                        Environment.SetEnvironmentVariable("MARQIUS_WKHTMLTOPDF", candidate);
                        return;
                    }
                }
                throw new InvalidOperationException("No program for the tests: run python3 build.py VERSION, or set MARQIUS_WKHTMLTOPDF.");
            }
        }

        internal static string Scratch()
        {
            var dir = Path.Combine(Path.GetTempPath(), "marqius-dinktopdf-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    /// <summary>What a PDF holds, read the simple way: enough to check what a test made.</summary>
    internal static class Pdf
    {
        internal static bool Is(byte[] bytes)
        {
            if (bytes == null || bytes.Length < 100 || Encoding.ASCII.GetString(bytes, 0, 5) != "%PDF-")
                return false;
            var tail = Encoding.ASCII.GetString(bytes, bytes.Length - Math.Min(64, bytes.Length), Math.Min(64, bytes.Length));
            return tail.Contains("%%EOF");
        }

        internal static int Pages(byte[] bytes)
        {
            var text = Encoding.GetEncoding(28591).GetString(bytes);
            return Regex.Matches(text, @"/Type\s*/Page(?![A-Za-z])").Count;
        }

        internal static bool HasPicture(byte[] bytes)
        {
            return Encoding.GetEncoding(28591).GetString(bytes).Contains("/Subtype /Image")
                || Encoding.GetEncoding(28591).GetString(bytes).Contains("/Subtype/Image");
        }

        // width and height of the first page's MediaBox, in points
        internal static (double Width, double Height) FirstPageSize(byte[] bytes)
        {
            var text = Encoding.GetEncoding(28591).GetString(bytes);
            var m = Regex.Match(text, @"/MediaBox\s*\[\s*([-\d.]+)\s+([-\d.]+)\s+([-\d.]+)\s+([-\d.]+)\s*\]");
            if (!m.Success)
                return (0, 0);
            double v(int i) => double.Parse(m.Groups[i].Value, System.Globalization.CultureInfo.InvariantCulture);
            return (v(3) - v(1), v(4) - v(2));
        }
    }

    internal static class Png
    {
        // a 2x2 red picture
        internal static readonly byte[] Red = Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAIAAAACCAIAAAD91JpzAAAAEElEQVR4nGP4z8AARAwQCgAf7gP9i18U1AAAAABJRU5ErkJggg==");
    }
}
