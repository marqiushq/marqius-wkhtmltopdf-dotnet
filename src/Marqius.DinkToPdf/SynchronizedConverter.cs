using DinkToPdf.Contracts;
using System;
using System.Threading;
using System.Collections.Concurrent;
using System.Reflection;
using System.Threading.Tasks;

namespace DinkToPdf
{
    /// <summary>The converter DinkToPdf's examples register for the whole app. With <see cref="PdfTools"/>, conversions run side by side (as many at once as there are processors); with other tools, one at a time on a thread of their own, as in DinkToPdf.</summary>
    public class SynchronizedConverter : BasicConverter
    {
        Thread conversionThread;

        BlockingCollection<Task> conversions = new BlockingCollection<Task>();

        bool kill = false;

        private readonly object startLock = new object();

        /// <summary>A converter for the whole app: <c>new SynchronizedConverter(new PdfTools())</c>.</summary>
        /// <param name="tools">The tools to work through: <c>new PdfTools()</c>.</param>
        public SynchronizedConverter(ITools tools) : base(tools)
        {

        }

        /// <summary>Converts the document: the PDF's bytes, or an empty array when it could not be made (the Error event says why).</summary>
        /// <param name="document">The document to convert: its settings for the whole PDF, and its pages.</param>
        public override byte[] Convert(IDocument document)
        {
            // marqius-wkhtmltopdf runs each conversion as a process of its own, so PdfTools needs no
            // single worker thread: conversions run side by side (as many at once as there are
            // processors). Other tools keep DinkToPdf's one thread.
            if (Tools is PdfTools)
            {
                return base.Convert(document);
            }
            return Invoke(() => base.Convert(document));
        }

        /// <summary>Runs a call on the converter's own thread, as DinkToPdf did for libwkhtmltox. Not needed with <see cref="PdfTools"/>.</summary>
        /// <typeparam name="TResult">What the call gives back.</typeparam>
        /// <param name="delegate">The call to run.</param>
        public TResult Invoke<TResult>(Func<TResult> @delegate)
        {
            StartThread();

            Task<TResult> task = new Task<TResult>(@delegate);

            lock (task)
            {
                //add task to blocking collection
                conversions.Add(task);

                //wait for task to be processed by conversion thread 
                Monitor.Wait(task);
            }

            //throw exception that happened during conversion
            if (task.Exception != null)
            {
                throw task.Exception;
            }

            return task.Result;
        }

        private void StartThread()
        {
            lock (startLock)
            {
                if (conversionThread == null)
                {
                    conversionThread = new Thread(Run)
                    {
                        IsBackground = true,
                        Name = "wkhtmltopdf worker thread"
                    };

                    kill = false;

                    conversionThread.Start();
                }
            }
        }
        
        private void StopThread()
        {
            lock (startLock)
            {
                if (conversionThread != null)
                {
                    kill = true;

                    while (conversionThread.ThreadState == ThreadState.Stopped)
                    { }

                    conversionThread = null;
                }
            }
        }

        private void Run()
        {
            while (!kill)
            {
                //get next conversion taks from blocking collection
                Task task = conversions.Take();

                lock (task)
                {
                    //run taks on thread that called RunSynchronously method
                    task.RunSynchronously();

                    //notify caller thread that task is completed
                    Monitor.Pulse(task);
                }
            }
        }
    }
}
