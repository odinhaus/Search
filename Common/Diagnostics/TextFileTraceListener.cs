using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.Diagnostics
{
    public class TextFileTraceListener : TextWriterTraceListener
    {
        static object _sync = new object();
        public TextFileTraceListener(string fileName)
        {
            var logFolder = AppContext.GetEnvironmentVariable("LogPath", "C:\\Logs");
            if (!Directory.Exists(logFolder))
            {
                Directory.CreateDirectory(logFolder);
            }
            var path = Path.Combine(logFolder, fileName);
            this.Writer = new StreamWriter(File.Open(path, FileMode.OpenOrCreate, FileAccess.Write, FileShare.ReadWrite));
        }
        public override void Fail(string message)
        {
            lock (_sync)
            {
                this.Writer.WriteLine(string.Format("Failure {0}: Message: {1}", Altus.Suffūz.CurrentTime.Now, message));
                this.Writer.Flush();
            }
        }

        public override void Fail(string message, string detailMessage)
        {
            lock (_sync)
            {
                this.Writer.WriteLine(string.Format("Failure {0}: Message: {1}, Details: {2}", Altus.Suffūz.CurrentTime.Now, message, detailMessage));
                this.Writer.Flush();
            }
        }

        public override void Write(object o)
        {
            lock (_sync)
            {
                this.Writer.Write(o.ToString());
                this.Writer.Flush();
            }
        }

        public override void Write(string message)
        {
            lock (_sync)
            {
                base.Writer.Write(message);
                this.Writer.Flush();
            }
        }

        public override void WriteLine(object o)
        {
            lock (_sync)
            {
                this.Writer.WriteLine(o.ToString());
                this.Writer.Flush();
            }
        }

        public override void WriteLine(string message)
        {
            lock (_sync)
            {
                this.Writer.WriteLine(message);
                this.Writer.Flush();
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (this.Writer != null)
            {
                this.Writer.Flush();
                this.Writer.Dispose();
                this.Writer = null;
            }
            base.Dispose(disposing);
        }
    }
}
