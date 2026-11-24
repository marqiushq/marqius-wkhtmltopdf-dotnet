using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace Marqius.Wkhtmltopdf
{
    /// <summary>
    /// marqius-wkhtmltopdf, the maintained drop-in replacement for wkhtmltopdf, for the computer this
    /// runs on. Give <see cref="Path"/> to any library that asks where wkhtmltopdf is.
    /// </summary>
    /// <example>
    /// <code>
    /// // NReco.PdfGenerator, Wkhtmltopdf.NetCore, or your own Process.Start:
    /// var wkhtmltopdf = Marqius.Wkhtmltopdf.Wkhtmltopdf.Path;
    /// </code>
    /// </example>
    public static class Wkhtmltopdf
    {
        /// <summary>The environment variable that names a program to use instead of the one in this
        /// package (one installed with apt, Homebrew or the install line, for instance).</summary>
        public const string PathVariable = "MARQIUS_WKHTMLTOPDF";

        private static readonly Lazy<string> path = new Lazy<string>(Find, LazyThreadSafetyMode.ExecutionAndPublication);
        private static readonly Lazy<string> cache = new Lazy<string>(WritableCache, LazyThreadSafetyMode.ExecutionAndPublication);

        /// <summary>The version of marqius-wkhtmltopdf this package carries, such as 0.12.14.</summary>
        public static string Version => Builds.Version;

        /// <summary>
        /// The full path of the program, ready to run: found beside the app (or in its runtimes folder),
        /// checked against the SHA-256 it was packed with, and made executable on Linux and macOS.
        /// </summary>
        /// <exception cref="PlatformNotSupportedException">This package has no program for this computer.</exception>
        /// <exception cref="FileNotFoundException">The program is not where a build or publish puts it.</exception>
        public static string Path => path.Value;

        /// <summary>
        /// The folder of this package that holds the program for this computer (win-x64, linux-x64,
        /// linux-arm64 or osx-arm64), or null when there is none: Windows on ARM runs the win-x64
        /// program, and Alpine and other musl systems run the linux one, which needs no C library.
        /// </summary>
        public static string RuntimeId
        {
            get
            {
                var arch = RuntimeInformation.OSArchitecture;
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    return arch == Architecture.X64 || arch == Architecture.Arm64 ? "win-x64" : null;
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                    return arch == Architecture.X64 ? "linux-x64" : arch == Architecture.Arm64 ? "linux-arm64" : null;
                if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                    return arch == Architecture.Arm64 ? "osx-arm64" : null;
                return null;
            }
        }

        private static bool IsWindows => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

        private static string FileName => IsWindows ? "marqius-wkhtmltopdf.exe" : "marqius-wkhtmltopdf";

        /// <summary>
        /// A start for the program with these arguments: no shell, standard input, output and error
        /// redirected, and a font cache it can keep (see <see cref="CacheFolder"/>).
        /// </summary>
        public static ProcessStartInfo StartInfo(IEnumerable<string> arguments)
        {
            var start = new ProcessStartInfo
            {
                FileName = Path,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardErrorEncoding = Encoding.UTF8,
            };
#if NET8_0_OR_GREATER
            foreach (var a in arguments)
                start.ArgumentList.Add(a);
#else
            start.Arguments = JoinArguments(arguments);
#endif
            var folder = CacheFolder;
            if (folder != null)
                start.Environment["MARQIUS_CACHE"] = folder;
            return start;
        }

        /// <summary>
        /// Where the program keeps its index of the computer's fonts, when the place it would choose
        /// cannot be written (an IIS application pool's identity, a container whose home is read only):
        /// a folder in the temporary folder. Null when the program's own choice works, or when
        /// MARQIUS_CACHE is set already. Without a kept index, each run reads every font again.
        /// </summary>
        public static string CacheFolder => cache.Value;

        /// <summary>
        /// Puts the names wkhtmltopdf and wkhtmltoimage (wkhtmltopdf.exe and wkhtmltoimage.exe on
        /// Windows) in a folder, each running marqius-wkhtmltopdf: for libraries that look for
        /// wkhtmltopdf in a folder of their own, such as Rotativa (wwwroot/Rotativa). A wkhtmltopdf
        /// already there is kept beside them as wkhtmltopdf.before-marqius. Does nothing when the
        /// names are there already for this version.
        /// </summary>
        /// <returns>The path of the name wkhtmltopdf in that folder.</returns>
        public static string Install(string folder)
        {
            Directory.CreateDirectory(folder);
            var name = System.IO.Path.Combine(folder, IsWindows ? "wkhtmltopdf.exe" : "wkhtmltopdf");
            if (File.Exists(name) && Says(name).Contains($"marqius-wkhtmltopdf {Version})"))
                return name;
            var start = StartInfo(new[] { "install", folder, "--force" });
            using (var p = Process.Start(start))
            {
                p.StandardInput.Close();
                var output = p.StandardOutput.ReadToEndAsync();
                var error = p.StandardError.ReadToEnd();
                p.WaitForExit();
                if (p.ExitCode != 0 || !File.Exists(name))
                    throw new InvalidOperationException($"marqius-wkhtmltopdf install {folder} ended with exit status {p.ExitCode}: {error}{output.Result}".Trim());
            }
            return name;
        }

        // what a program says to -V (empty when it does not start)
        private static string Says(string program)
        {
            try
            {
                var start = new ProcessStartInfo(program, "-V")
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                };
                using (var p = Process.Start(start))
                {
                    var text = p.StandardOutput.ReadToEnd();
                    p.WaitForExit(10000);
                    return text;
                }
            }
            catch (Exception)
            {
                return "";
            }
        }

        /// <summary>Quotes arguments for a command line, as Windows programs and .NET read them.</summary>
        public static string JoinArguments(IEnumerable<string> arguments)
        {
            var line = new StringBuilder();
            foreach (var a in arguments)
            {
                if (line.Length > 0)
                    line.Append(' ');
                line.Append(Quote(a ?? ""));
            }
            return line.ToString();
        }

        internal static string Quote(string a)
        {
            if (a.Length > 0 && a.IndexOfAny(new[] { ' ', '\t', '\n', '\v', '"' }) < 0)
                return a;
            var s = new StringBuilder("\"");
            int backslashes = 0;
            foreach (var c in a)
            {
                if (c == '\\')
                {
                    backslashes++;
                    continue;
                }
                if (c == '"')
                {
                    s.Append('\\', backslashes * 2 + 1).Append('"');
                }
                else
                {
                    s.Append('\\', backslashes).Append(c);
                }
                backslashes = 0;
            }
            return s.Append('\\', backslashes * 2).Append('"').ToString();
        }

        private static string Find()
        {
            var named = Environment.GetEnvironmentVariable(PathVariable);
            if (!string.IsNullOrEmpty(named))
            {
                if (!File.Exists(named))
                    throw new FileNotFoundException($"{PathVariable} names {named}, and there is no file there.", named);
                return named;
            }
            var rid = RuntimeId ?? throw new PlatformNotSupportedException(
                $"marqius-wkhtmltopdf has no program for {RuntimeInformation.OSDescription} on {RuntimeInformation.OSArchitecture}. " +
                "It runs on Windows (x64, and ARM through x64), Linux x64 and ARM64 (Alpine too) and macOS on Apple silicon. " +
                $"Set {PathVariable} to a program you have, or write to hello@marqius.com.");
            var looked = new List<string>();
            foreach (var folder in Folders(rid))
            {
                string candidate;
                try
                {
                    candidate = System.IO.Path.GetFullPath(System.IO.Path.Combine(folder, FileName));
                }
                catch (Exception)
                {
                    continue;
                }
                if (looked.Contains(candidate))
                    continue;
                looked.Add(candidate);
                if (File.Exists(candidate))
                    return Ready(candidate, rid);
            }
            throw new FileNotFoundException(
                $"marqius-wkhtmltopdf ({FileName}, {rid}) was not found beside the app. Looked in: {string.Join(", ", looked)}. " +
                "A build or publish copies it there from the package Marqius.Wkhtmltopdf; " +
                $"for a publish to another runtime, run dotnet publish -r {rid}, or set {PathVariable} to the program.", FileName);
        }

        private static IEnumerable<string> Folders(string rid)
        {
            var app = AppContext.BaseDirectory ?? "";
            // a build or publish for one runtime puts it beside the app; one for every runtime, in runtimes/
            yield return app;
            yield return System.IO.Path.Combine(app, "runtimes", rid, "native");
            // the folders the .NET host searches for native files (a single-file app extracts them there)
            if (AppContext.GetData("NATIVE_DLL_SEARCH_DIRECTORIES") is string searched)
            {
                foreach (var d in searched.Split(System.IO.Path.PathSeparator))
                {
                    if (d.Length > 0)
                        yield return d;
                }
            }
            // beside this assembly: a .NET Framework app, or this package's own folder in the NuGet cache
            var here = System.IO.Path.GetDirectoryName(typeof(Wkhtmltopdf).Assembly.Location);
            if (!string.IsNullOrEmpty(here))
            {
                yield return here;
                yield return System.IO.Path.Combine(here, "runtimes", rid, "native");
                yield return System.IO.Path.Combine(here, "..", "..", "runtimes", rid, "native");
            }
        }

        private static string Ready(string program, string rid)
        {
            var expected = Builds.Sha256(rid);
            if (expected != null)
            {
                var got = Sha256(program);
                if (!string.Equals(got, expected, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException(
                        $"{program} is not the marqius-wkhtmltopdf {Version} this package carries (SHA-256 {got}, expected {expected}). " +
                        $"Restore the package again, or set {PathVariable} to the program you mean to use.");
            }
            if (IsWindows || Executable.Is(program))
                return program;
            if (Executable.TryMake(program) && Executable.Is(program))
                return program;
            // a file this user may not change (a container image that runs as a user other than the
            // one that copied the app in): a copy of its own, made executable
            var folder = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"marqius-wkhtmltopdf-{Version}-{rid}-{UserTag()}");
            Directory.CreateDirectory(folder);
            var copy = System.IO.Path.Combine(folder, FileName);
            if (!(File.Exists(copy) && new FileInfo(copy).Length == new FileInfo(program).Length && Executable.Is(copy)
                  && (expected == null || string.Equals(Sha256(copy), expected, StringComparison.OrdinalIgnoreCase))))
            {
                var temporary = copy + "." + Guid.NewGuid().ToString("N");
                File.Copy(program, temporary, true);
                if (!Executable.TryMake(temporary))
                    throw new UnauthorizedAccessException($"marqius-wkhtmltopdf could not be made executable, at {program} or at {temporary}.");
                if (File.Exists(copy))
                    File.Delete(copy);
                File.Move(temporary, copy);
            }
            return copy;
        }

        private static string UserTag()
        {
            try
            {
                return Environment.UserName.Replace(System.IO.Path.DirectorySeparatorChar, '_');
            }
            catch (Exception)
            {
                return "user";
            }
        }

        private static string Sha256(string file)
        {
            using (var sha = SHA256.Create())
            using (var stream = File.OpenRead(file))
            {
                var hash = sha.ComputeHash(stream);
                var hex = new StringBuilder(hash.Length * 2);
                foreach (var b in hash)
                    hex.Append(b.ToString("x2"));
                return hex.ToString();
            }
        }

        // the folder the program would keep its font index in (as the program chooses it), and whether
        // it can be written; if not, a folder in the temporary folder
        private static string WritableCache()
        {
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("MARQIUS_CACHE")))
                return null;
            string own = null;
            try
            {
                if (IsWindows)
                {
                    var local = Environment.GetEnvironmentVariable("LOCALAPPDATA");
                    own = string.IsNullOrEmpty(local) ? null : System.IO.Path.Combine(local, "marqius");
                }
                else
                {
                    var home = Environment.GetEnvironmentVariable("HOME");
                    var xdg = Environment.GetEnvironmentVariable("XDG_CACHE_HOME");
                    if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                        own = string.IsNullOrEmpty(home) ? null : System.IO.Path.Combine(home, "Library", "Caches", "marqius");
                    else if (!string.IsNullOrEmpty(xdg))
                        own = System.IO.Path.Combine(xdg, "marqius");
                    else
                        own = string.IsNullOrEmpty(home) ? null : System.IO.Path.Combine(home, ".cache", "marqius");
                }
                if (own != null && CanWrite(own))
                    return null;
            }
            catch (Exception)
            {
                // the temporary folder below
            }
            var temporary = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"marqius-cache-{UserTag()}");
            return CanWrite(temporary) ? temporary : null;
        }

        private static bool CanWrite(string folder)
        {
            try
            {
                Directory.CreateDirectory(folder);
                var probe = System.IO.Path.Combine(folder, ".write-" + Guid.NewGuid().ToString("N"));
                File.WriteAllText(probe, "");
                File.Delete(probe);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }

    /// <summary>The execute permission on Linux and macOS, which NuGet does not keep. Called only
    /// off Windows (Wkhtmltopdf.Ready returns before it there).</summary>
#pragma warning disable CA1416
    internal static class Executable
    {
        // UserExecute | GroupExecute | OtherExecute, as System.IO.UnixFileMode numbers them
        private const int AnyExecute = 64 | 8 | 1;

        internal static bool Is(string file)
        {
#if NET8_0_OR_GREATER
            return ((int)File.GetUnixFileMode(file) & AnyExecute) != 0;
#else
            var mode = UnixMode(file);
            if (mode.HasValue)
                return (mode.Value & AnyExecute) != 0;
            return Run("test", "-x", file);
#endif
        }

        internal static bool TryMake(string file)
        {
            try
            {
#if NET8_0_OR_GREATER
                File.SetUnixFileMode(file, File.GetUnixFileMode(file) | (UnixFileMode)(AnyExecute | 256 | 32 | 4));
                return true;
#else
                var set = typeof(File).GetMethod("SetUnixFileMode", new[] { typeof(string), Type.GetType("System.IO.UnixFileMode") ?? typeof(int) });
                var mode = UnixMode(file);
                if (set != null && mode.HasValue)
                {
                    var unixFileMode = Type.GetType("System.IO.UnixFileMode");
                    set.Invoke(null, new object[] { file, Enum.ToObject(unixFileMode, mode.Value | AnyExecute | 256 | 32 | 4) });
                    return true;
                }
                return Run("chmod", "+x", file);
#endif
            }
            catch (Exception)
            {
                return false;
            }
        }

#if !NET8_0_OR_GREATER
        // File.GetUnixFileMode where the runtime has it (.NET 7 and later), read without compiling against it
        private static int? UnixMode(string file)
        {
            var unixFileMode = Type.GetType("System.IO.UnixFileMode");
            var get = unixFileMode == null ? null : typeof(File).GetMethod("GetUnixFileMode", new[] { typeof(string) });
            if (get == null)
                return null;
            return Convert.ToInt32(get.Invoke(null, new object[] { file }));
        }

        private static bool Run(string command, params string[] arguments)
        {
            try
            {
                var start = new ProcessStartInfo(command, Wkhtmltopdf.JoinArguments(arguments))
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                };
                using (var p = Process.Start(start))
                {
                    p.WaitForExit(10000);
                    return p.HasExited && p.ExitCode == 0;
                }
            }
            catch (Win32Exception)
            {
                return false;
            }
        }
#endif
    }
}
#pragma warning restore CA1416
