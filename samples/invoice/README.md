# The invoice on the package's page

This folder makes the invoice shown on [Marqius.DinkToPdf](https://www.nuget.org/packages/Marqius.DinkToPdf)'s page, from HTML and CSS, with Marqius.DinkToPdf.

```bash
dotnet run
```

Then open `invoice.pdf`. The design is in `templates/invoice.html` and `templates/style.css`; the data is in `Program.cs`. [The manual](https://github.com/marqiushq/marqius-wkhtmltopdf-dotnet/blob/main/docs/manual.md#3-an-html-template-filled-with-your-data) explains each part.
