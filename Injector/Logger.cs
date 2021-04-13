using System;
using System.IO;

namespace Injector
{
    internal class Logger
    {
        public enum LoggingLevel
        {
            DEBUG,
            INFO,
            WARN,
            ERROR
        }

        private const string LOG_FILENAME = "injector.log";
        private static Logger _logger;

        private Logger()
        {
            InternalLog(LoggingLevel.DEBUG, "Logger initialized", append: false);
        }

        public static Logger Instance()
        {
            if (_logger == null)
            {
                _logger = new Logger();
            }
            return _logger;
        }

        private void InternalLog(LoggingLevel level, string message, bool quiet = false, bool append = true)
        {
            if (level == LoggingLevel.ERROR)
            {
                Console.Error.WriteLine(message);
            }
            else if ((Program.Options?.Verbose ?? false) || !quiet && level != LoggingLevel.DEBUG)
            {
                Console.WriteLine(message);
            }

            using (var w = new StreamWriter(LOG_FILENAME, append))
            {
                w.WriteLine($"{DateTime.Now.ToString("s")} | {level.ToString("g")}: {message}");
            }
        }

        public void Log(LoggingLevel level, string message, bool quiet)
        {
            InternalLog(level, message, quiet);
        }

        public void Log(LoggingLevel level, string message)
        {
            Log(level, message, Program.Options?.Quiet ?? false);
        }

        public void Debug(string message)
        {
            Log(LoggingLevel.DEBUG, message);
        }

        public void Info(string message, bool quiet)
        {
            Log(LoggingLevel.INFO, message, quiet);
        }

        public void Info(string message)
        {
            Info(message, Program.Options?.Quiet ?? false);
        }

        public void Warn(string message, bool quiet)
        {
            Log(LoggingLevel.WARN, message, quiet);
        }

        public void Warn(string message)
        {
            Warn(message, Program.Options?.Quiet ?? false);
        }

        public void Error(string message)
        {
            Log(LoggingLevel.ERROR, message);
        }
    }
}