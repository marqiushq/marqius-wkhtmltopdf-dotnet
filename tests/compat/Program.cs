using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DinkToPdf;

// Makes every document of Cases into OUT, with DinkToPdf as this build carries it (the original with
// libwkhtmltox, or Marqius.DinkToPdf): dotnet run -- WORK OUT [NAME...]. A line per document, and
// messages.txt with each document's warnings and errors. With names, only those documents are made
// (messages in messages-alone.txt): tests/compat.py makes some alone, in a new process.
var work = Path.GetFullPath(args[0]);
var output = Path.GetFullPath(args[1]);
Directory.CreateDirectory(work);
Directory.CreateDirectory(output);
var only = new HashSet<string>(args.Skip(2));
Cases.Files(work);
var converter = new SynchronizedConverter(new PdfTools());
var messages = new System.Text.StringBuilder();
string current = "";
converter.Warning += (s, e) => messages.AppendLine($"{current}\twarning\t{e.Message.Replace('\n', ' ')}");
converter.Error += (s, e) => messages.AppendLine($"{current}\terror\t{e.Message.Replace('\n', ' ')}");
int made = 0;
foreach (var (name, document) in Cases.All(work))
{
    if (only.Count > 0 && !only.Contains(name))
        continue;
    current = name;
    var started = DateTime.UtcNow;
    var pdf = converter.Convert(document);
    var ms = (DateTime.UtcNow - started).TotalMilliseconds;
    if (pdf.Length > 0)
    {
        File.WriteAllBytes(Path.Combine(output, name + ".pdf"), pdf);
        made++;
    }
    Console.WriteLine($"{name}\t{(pdf.Length > 0 ? "made" : "NOT MADE")}\t{pdf.Length} bytes\t{ms:0} ms");
}
File.WriteAllText(Path.Combine(output, only.Count > 0 ? "messages-alone.txt" : "messages.txt"), messages.ToString());
Console.WriteLine($"{made} of {(only.Count > 0 ? only.Count : Cases.All(work).Count())} documents made; {converter.Tools.GetLibraryVersion()}");
