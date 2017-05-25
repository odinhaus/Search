using Altus.Suffūz;
using Common.Extensions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.Diagnostics
{
    public static partial class Logger
    {
        //static EventLog _eventLog;
        static object _sync = new object();
        public static readonly string DIVIDER = System.Environment.NewLine + "========================================================================================" + System.Environment.NewLine;

        static Logger()
        {
            TraceSwitch ts = new TraceSwitch("TraceLevelSwitch", "Determines the tracing level to log/display");
            TraceLevel = ts.Level;
            //try
            //{
            //    _eventLog = new EventLog();
            //    _eventLog.Source = AppContext.Name;
            //    _eventLog.Log = "SHS";
            //}
            //catch { }
            //try
            //{
            //    _eventLog.MaximumKilobytes = 200 * 1024;
            //    _eventLog.ModifyOverflowPolicy(OverflowAction.OverwriteAsNeeded, 0);
            //}
            //catch
            //{
            //}
            Trace.Listeners.Clear();
            //Trace.Listeners.Add(new EventLogTraceListener(_eventLog));
            Trace.Listeners.Add(new ConsoleTraceListener(true));
            Trace.Listeners.Add(new DefaultTraceListener());
            Trace.Listeners.Add(new TextFileTraceListener(AppContext.Name + ".log"));
        }

        public static TraceLevel TraceLevel { get; set; }

        public static void Log(string message)
        {
            if (TraceLevel < TraceLevel.Verbose) return;
            lock (_sync)
            {
                //Trace.TraceInformation(message);
                Trace.WriteLine(message.Left(16250));
            }
        }

        public static void Log(Exception exception)
        {
            Log(exception, "An error treated as Verbose Information occurred.");
        }

        public static void Log(Exception exception, string headerMessage)
        {
            if (TraceLevel < TraceLevel.Verbose) return;
            Exception inner = exception;
            lock (_sync)
            {
                while (inner != null)
                {
                    //Trace.TraceInformation("{0}\r\nSource: {1}\r\nMessage: {2}\r\nStack Trace: {3}",
                    //    headerMessage,
                    //    exception.Source,
                    //    exception.Message,
                    //    exception.StackTrace);
                    string message = String.Format("{0}\r\nSource: {1}\r\nMessage: {2}\r\nStack Trace: {3}",
                        headerMessage,
                        exception.Source,
                        exception.Message,
                        exception.StackTrace);
                    Log(message);
                    inner = inner.InnerException;
                }
            }
        }

        public static void LogInfo(string message, bool addDivider = true)
        {
            if (TraceLevel < TraceLevel.Info) return;
            lock (_sync)
            {
                if (addDivider)
                    Trace.TraceInformation(CurrentTime.Now.ToISO8601() + System.Environment.NewLine + DIVIDER + message.Left(16250) + DIVIDER);
                else
                    Trace.TraceInformation(CurrentTime.Now.ToISO8601() + System.Environment.NewLine + message.Left(16250));
            }
        }

        public static void LogInfo(Exception exception)
        {
            Log(exception, "An error treated as Information occurred.");
        }

        public static void LogInfo(Exception exception, string headerMessage, bool addDivider = true)
        {
            if (TraceLevel < TraceLevel.Info) return;
            Exception inner = exception;
            lock (_sync)
            {
                while (inner != null)
                {
                    if (addDivider)
                    {
                        Trace.TraceInformation(CurrentTime.Now.ToISO8601() + System.Environment.NewLine + DIVIDER + "{0}\r\nSource: {1}\r\nMessage: {2}\r\nStack Trace: {3}" + DIVIDER,
                            headerMessage,
                            exception.Source,
                            exception.Message,
                            exception.StackTrace);
                    }
                    else
                    {
                        Trace.TraceInformation(CurrentTime.Now.ToISO8601() + System.Environment.NewLine + "{0}\r\nSource: {1}\r\nMessage: {2}\r\nStack Trace: {3}",
                            headerMessage,
                            exception.Source,
                            exception.Message,
                            exception.StackTrace);
                    }
                    inner = inner.InnerException;
                }
            }
        }


        public static void LogWarn(string message, bool addDivider = true)
        {
            if (TraceLevel < TraceLevel.Warning) return;
            lock (_sync)
            {
                if (addDivider)
                    Trace.TraceWarning(CurrentTime.Now.ToISO8601() + System.Environment.NewLine + DIVIDER + message.Left(16250) + DIVIDER);
                else
                    Trace.TraceWarning(CurrentTime.Now.ToISO8601() + System.Environment.NewLine + message);
            }
        }

        public static void LogWarn(Exception exception)
        {
            LogWarn(exception, "An error treated as Warning occurred.");
        }

        public static void LogWarn(Exception exception, string headerMessage, bool addDivider = true)
        {
            if (TraceLevel < TraceLevel.Warning) return;
            Exception inner = exception;
            lock (_sync)
            {
                while (inner != null)
                {
                    if (addDivider)
                    {
                        Trace.TraceWarning(CurrentTime.Now.ToISO8601() + System.Environment.NewLine + DIVIDER + "{0}\r\nSource: {1}\r\nMessage: {2}\r\nStack Trace: {3}" + DIVIDER,
                            headerMessage,
                            exception.Source,
                            exception.Message,
                            exception.StackTrace);
                    }
                    else
                    {
                        Trace.TraceWarning(CurrentTime.Now.ToISO8601() + System.Environment.NewLine + "{0}\r\nSource: {1}\r\nMessage: {2}\r\nStack Trace: {3}",
                            headerMessage,
                            exception.Source,
                            exception.Message,
                            exception.StackTrace);
                    }
                    inner = inner.InnerException;
                }
            }
        }



        public static void LogError(string message, bool addDivider = true)
        {
            if (TraceLevel < TraceLevel.Error) return;
            lock (_sync)
            {
                if (addDivider)
                    Trace.TraceError(CurrentTime.Now.ToISO8601() + System.Environment.NewLine + DIVIDER + message.Left(16250) + DIVIDER);
                else
                    Trace.TraceError(CurrentTime.Now.ToISO8601() + System.Environment.NewLine + message);
            }
        }

        public static void LogError(Exception exception)
        {
            LogError(exception, "An Error occurred.");
        }

        public static void LogError(Exception exception, string headerMessage, bool addDivider = true)
        {
            if (TraceLevel < TraceLevel.Error) return;
            Exception inner = exception;
            lock (_sync)
            {
                while (inner != null)
                {
                    if (addDivider)
                    {
                        Trace.TraceError(CurrentTime.Now.ToISO8601() + System.Environment.NewLine + DIVIDER + "{0}\r\nSource: {1}\r\nMessage: {2}\r\nStack Trace: {3}" + DIVIDER,
                            headerMessage,
                            exception.Source,
                            exception.Message,
                            exception.StackTrace);
                    }
                    else
                    {
                        Trace.TraceError(CurrentTime.Now.ToISO8601() + System.Environment.NewLine + "{0}\r\nSource: {1}\r\nMessage: {2}\r\nStack Trace: {3}",
                            headerMessage,
                            exception.Source,
                            exception.Message,
                            exception.StackTrace);
                    }
                    inner = inner.InnerException;
                }
            }
        }
    }
}
