using DinkToPdf;

// An app with Marqius.DinkToPdf, using a library (Reports) that was built against DinkToPdf 1.0.8.
var converter = new SynchronizedConverter(new PdfTools());
var heard = new List<string>();
var pdf = Reports.Invoices.Make(converter, "42", heard);
File.WriteAllBytes("invoice.pdf", pdf);
var assembly = typeof(PdfTools).Assembly.GetName();
Console.WriteLine($"assembly: {assembly.Name} {assembly.Version}");
Console.WriteLine($"raw: {Reports.Invoices.Raw()}");
Console.WriteLine($"thread: {Reports.Invoices.OnTheConverterThread(converter) != Environment.CurrentManagedThreadId}");
foreach (var line in heard)
    Console.WriteLine(line);
