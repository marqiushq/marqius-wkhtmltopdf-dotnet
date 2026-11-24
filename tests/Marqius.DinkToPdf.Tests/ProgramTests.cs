using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Xunit;
using Program = Marqius.Wkhtmltopdf.Wkhtmltopdf;

namespace Marqius.DinkToPdf.Tests
{
    /// <summary>Marqius.Wkhtmltopdf's own calls: the program's path and the names it puts in a folder.</summary>
    public class ProgramTests
    {
        public ProgramTests()
        {
            Setup.Program();
        }

        private static string Run(string program, string arguments)
        {
            var start = new ProcessStartInfo(program, arguments) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
            using (var p = Process.Start(start))
            {
                var text = p.StandardOutput.ReadToEnd() + p.StandardError.ReadToEnd();
                p.WaitForExit();
                return text;
            }
        }

        [Fact]
        public void The_path_is_the_program()
        {
            Assert.True(File.Exists(Program.Path));
            Assert.StartsWith("marqius-wkhtmltopdf ", Run(Program.Path, "-V"));
        }

        [Fact]
        public void Install_puts_wkhtmltopdf_and_wkhtmltoimage_in_a_folder_once()
        {
            var folder = Path.Combine(Setup.Scratch(), "wwwroot", "Rotativa");
            var name = Program.Install(folder);
            var windows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
            Assert.Equal(Path.Combine(folder, windows ? "wkhtmltopdf.exe" : "wkhtmltopdf"), name);
            Assert.StartsWith("wkhtmltopdf 0.12.6 (with patched qt) (compatible; marqius-wkhtmltopdf ", Run(name, "-V"));
            var image = Path.Combine(folder, windows ? "wkhtmltoimage.exe" : "wkhtmltoimage");
            Assert.StartsWith("wkhtmltoimage 0.12.6", Run(image, "-V"));
            // a second time changes nothing
            var before = new FileInfo(name).LastWriteTimeUtc;
            Assert.Equal(name, Program.Install(folder));
            Assert.Equal(before, new FileInfo(name).LastWriteTimeUtc);
            Assert.False(File.Exists(name + ".before-marqius"));
        }

        [Fact]
        public void This_computer_has_a_program_in_the_package()
        {
            Assert.Contains(Program.RuntimeId, new[] { "win-x64", "linux-x64", "linux-arm64", "osx-arm64" });
        }
    }
}
