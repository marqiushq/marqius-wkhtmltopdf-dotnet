using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using DinkToPdf.Contracts;
using DinkToPdf.EventDefinitions;
using System.Globalization;

namespace DinkToPdf
{
    /// <summary>Turns HTML into PDF with marqius-wkhtmltopdf. Make one for the whole app and share it: conversions run side by side, each in a process of its own.</summary>
    public class BasicConverter : IConverter
    {
        /// <summary>The tools this converter works through: a <see cref="PdfTools"/>.</summary>
        public readonly ITools Tools;

        /// <summary>The document this converter was last given.</summary>
        public IDocument ProcessingDocument { get; private set; }

        // marqius: PDFs are made side by side, but this converter's event handlers are called one at
        // a time, as DinkToPdf's single worker thread called them, so handlers written for it stay safe
        private readonly object eventLock = new object();

        /// <summary>Raised when a conversion moves to its next phase, such as "Loading pages" or "Printing pages".</summary>
        public event EventHandler<PhaseChangedArgs> PhaseChanged;

        /// <summary>Raised as a conversion goes on, with text such as "Page 1 of 3".</summary>
        public event EventHandler<ProgressChangedArgs> ProgressChanged;

        /// <summary>Raised when a conversion ends, saying whether the PDF was made.</summary>
        public event EventHandler<FinishedArgs> Finished;

        /// <summary>Raised when something stops a PDF from being made, such as a page that cannot be loaded or no fonts. With no handler, and no PDF made, the message is written to standard error (DinkToPdf said nothing).</summary>
        public event EventHandler<ErrorArgs> Error;

        /// <summary>Raised for something worth knowing that did not stop the PDF, such as a picture that could not be loaded, or letters no installed font has (with the command that downloads one).</summary>
        public event EventHandler<WarningArgs> Warning;

        /// <summary>A converter that works through these tools: <c>new BasicConverter(new PdfTools())</c>.</summary>
        public BasicConverter(ITools tools) {
            Tools = tools;
        }

        /// <summary>Converts the document: the PDF's bytes, or an empty array when it could not be made (the <see cref="Error"/> event says why). With <see cref="GlobalSettings.Out"/> set, the PDF is written to that file, and the array is empty.</summary>
        public virtual byte[] Convert(IDocument document)
        {
            if (document.GetObjects().Count() == 0)
            {
                throw new ArgumentException("No objects is defined in document that was passed. At least one object must be defined.");
            }

            ProcessingDocument = document;

            byte[] result = new byte[0];
            Tools.Load();

            IntPtr converter = CreateConverter(document);
            var unheard = Register(converter, document);

            try
            {
                bool converted = Tools.DoConversion(converter);

                if (converted)
                {
                    result = Tools.GetConversionResult(converter);
                }
                else
                {
                    Unheard(unheard);
                }
            }
            finally
            {
                Tools.DestroyConverter(converter);
            }

            return result;
        }

        // register events: each conversion's events name its own document (marqius: conversions may run
        // at the same time, each in its own process). Errors no handler takes are kept, for Unheard.
        private List<string> Register(IntPtr converter, IDocument document)
        {
            var unheard = new List<string>();
            Tools.SetPhaseChangedCallback(converter, c => OnPhaseChanged(c, document));
            Tools.SetProgressChangedCallback(converter, c => OnProgressChanged(c, document));
            Tools.SetFinishedCallback(converter, (c, success) => OnFinished(c, success, document));
            Tools.SetWarningCallback(converter, (c, message) => OnWarning(c, message, document));
            Tools.SetErrorCallback(converter, (c, message) => OnError(c, message, document, unheard));
            return unheard;
        }

        // Convert, without holding a thread while the PDF is made (for Marqius.DinkToPdf's ConvertAsync):
        // the same result and events. Internal, so it is never part of DinkToPdf's own API.
        internal async System.Threading.Tasks.Task<byte[]> ConvertAsyncCore(IDocument document, System.Threading.CancellationToken cancellationToken)
        {
            if (!(Tools is PdfTools tools))
            {
                // other tools have no way to wait without a thread: run Convert on one
                return await System.Threading.Tasks.Task.Run(() => Convert(document), cancellationToken).ConfigureAwait(false);
            }
            if (document.GetObjects().Count() == 0)
            {
                throw new ArgumentException("No objects is defined in document that was passed. At least one object must be defined.");
            }

            ProcessingDocument = document;

            byte[] result = new byte[0];
            Tools.Load();

            IntPtr converter = CreateConverter(document);
            var unheard = Register(converter, document);

            try
            {
                if (await tools.DoConversionAsync(converter, cancellationToken).ConfigureAwait(false))
                {
                    result = Tools.GetConversionResult(converter);
                }
                else
                {
                    Unheard(unheard);
                }
            }
            finally
            {
                Tools.DestroyConverter(converter);
            }

            return result;
        }

        private void OnPhaseChanged(IntPtr converter, IDocument document)
        {
            int currentPhase = Tools.GetCurrentPhase(converter);
            var eventArgs = new PhaseChangedArgs()
            {
                Document = document,
                PhaseCount = Tools.GetPhaseCount(converter),
                CurrentPhase = currentPhase,
                Description = Tools.GetPhaseDescription(converter, currentPhase)
            };

            lock (eventLock) PhaseChanged?.Invoke(this, eventArgs);
        }

        private void OnProgressChanged(IntPtr converter, IDocument document)
        {
            var eventArgs = new ProgressChangedArgs()
            {
                Document = document,
                Description = Tools.GetProgressString(converter)
            };

            lock (eventLock) ProgressChanged?.Invoke(this, eventArgs);
        }

        private void OnFinished(IntPtr converter, int success, IDocument document)
        {
            var eventArgs = new FinishedArgs()
            {
                Document = document,
                Success = success == 1 ? true : false
            };

            lock (eventLock) Finished?.Invoke(this, eventArgs);
        }

        private void OnError(IntPtr converter, string message, IDocument document, List<string> unheard)
        {
            var eventArgs = new ErrorArgs()
            {
                Document = document,
                Message = message
            };

            var handlers = Error;
            if (handlers != null)
                lock (eventLock) handlers(this, eventArgs);
            else
                lock (unheard) unheard.Add(message);
        }

        private void OnWarning(IntPtr converter, string message, IDocument document)
        {
            var eventArgs = new WarningArgs()
            {
                Document = document,
                Message = message
            };

            lock (eventLock) Warning?.Invoke(this, eventArgs);
        }

        // marqius: when no PDF was made and no handler heard why, the reason goes to standard error, so
        // a server's log shows it (no fonts, a page not found). A PDF that was made prints nothing, and
        // warnings never print, as in DinkToPdf.
        private static void Unheard(List<string> unheard)
        {
            string[] messages;
            lock (unheard) messages = unheard.ToArray();
            try
            {
                foreach (var message in messages)
                    Console.Error.WriteLine($"DinkToPdf (error): {message}");
            }
            catch (Exception)
            {
                // no standard error to write to
            }
        }

        private IntPtr CreateConverter(IDocument document) {

            IntPtr converter = IntPtr.Zero;

            {
                IntPtr settings = Tools.CreateGlobalSettings();
                
                ApplyConfig(settings, document, true);

                converter = Tools.CreateConverter(settings);
            }
            
            foreach (var obj in document.GetObjects())
            {
                if (obj != null)
                {
                    IntPtr settings = Tools.CreateObjectSettings();

                    ApplyConfig(settings, obj, false);

                    Tools.AddObject(converter, settings, obj.GetContent());
                }
            }
            
            return converter;
        }

#if NET5_0_OR_GREATER
        // the settings are read by reflection: a trimmed or Native AOT app keeps every one of their properties
        private const System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes Members =
            System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicProperties | System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.NonPublicProperties;

        [System.Diagnostics.CodeAnalysis.DynamicDependency(Members, typeof(GlobalSettings))]
        [System.Diagnostics.CodeAnalysis.DynamicDependency(Members, typeof(ObjectSettings))]
        [System.Diagnostics.CodeAnalysis.DynamicDependency(Members, typeof(WebSettings))]
        [System.Diagnostics.CodeAnalysis.DynamicDependency(Members, typeof(HeaderSettings))]
        [System.Diagnostics.CodeAnalysis.DynamicDependency(Members, typeof(FooterSettings))]
        [System.Diagnostics.CodeAnalysis.DynamicDependency(Members, typeof(LoadSettings))]
        [System.Diagnostics.CodeAnalysis.DynamicDependency(Members, typeof(MarginSettings))]
        [System.Diagnostics.CodeAnalysis.DynamicDependency(Members, typeof(HtmlToPdfDocument))]
        [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("Trimming", "IL2075", Justification = "DinkToPdf's settings types are kept whole by the DynamicDependency attributes above.")]
#endif
        private void ApplyConfig(IntPtr config, ISettings settings, bool isGlobal) {

            if (settings == null)
            {
                return;
            }

            var bindingFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            var props = settings.GetType().GetProperties(bindingFlags);

            foreach (var prop in props)
            {
                Attribute[] attrs = (Attribute[])prop.GetCustomAttributes();
                object propValue = prop.GetValue(settings);

                if (propValue == null)
                {
                    continue;
                }
                else if (attrs.Length > 0 && attrs[0] is WkHtmlAttribute)
                {
                    var attr = attrs[0] as WkHtmlAttribute;

                    Apply(config, attr.Name, propValue, isGlobal);
                }
                else if (propValue is ISettings)
                {
                    ApplyConfig(config, propValue as ISettings, isGlobal);
                }

            }
        }

        private void Apply(IntPtr config, string name, object value, bool isGlobal)
        {
            var type = value.GetType();

            Func<IntPtr, string, string, int> applySetting;
            if (isGlobal)
            {
                applySetting = Tools.SetGlobalSetting;
            }
            else
            {
                applySetting = Tools.SetObjectSetting;
            }

            if (typeof(bool) == type)
            {
                applySetting(config, name, ((bool)value == true ? "true" : "false"));
            }
            else if (typeof(double) == type)
            {
                applySetting(config, name, ((double)value).ToString("0.##", CultureInfo.InvariantCulture));
            }
            else if (typeof(Dictionary<string, string>).IsAssignableFrom(type))
            {
                var dictionary = (Dictionary<string, string>)value;
                int index = 0;

                foreach (var pair in dictionary)
                {
                    if (pair.Key == null || pair.Value == null)
                    {
                        continue;
                    }

                    //https://github.com/wkhtmltopdf/wkhtmltopdf/blob/c754e38b074a75a51327df36c4a53f8962020510/src/lib/reflect.hh#L192
                    applySetting(config, name + ".append", null);
                    applySetting(config, string.Format("{0}[{1}]", name, index), pair.Key + "\n" + pair.Value);

                    index++;
                }
            }
            else
            {
                applySetting(config, name, value.ToString());
            }
            
        }
    }
}
