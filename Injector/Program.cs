using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using CommandLine;

namespace Injector {
    class Program {
        private static Logger logger = Logger.Instance();

        static void Main(string[] args) {
            // dotnet publish -r win-x64 -c Release /p:PublishSingleFile=true
            try {
                Parser.Default.ParseArguments<InjectorOptions>(args)
                    .WithParsed(opts => {
                        logger.Options = opts;
                        Injector injector = new Injector(opts);
                        injector.Inject();
                    });
            } catch (Exception ex) {
                HandleException("An unknown error occurred. See log for details", ex);
            }
        }

        private static void InternalErrorHandler(string errorMessage, string debugMessage = null) {
            logger.Error(errorMessage);
            if (!string.IsNullOrEmpty(debugMessage)) {
                logger.Debug(debugMessage);
            }

            if (!logger.Options?.IsNonInteractive ?? true) {
                Console.WriteLine("Press any key to exit...");
                Console.ReadKey(true);
            }

            logger.Debug("Exiting...");
            Environment.Exit(1);
        }

        public static void HandleWin32Error(string errorMessage) {
            var exception = new Win32Exception(Marshal.GetLastWin32Error());
            string debugMessage = $"Code: {exception.ErrorCode}, Message: {exception.Message}";
            InternalErrorHandler(errorMessage, debugMessage);
        }

        public static void HandleError(string errorMessage) {
            InternalErrorHandler(errorMessage);
        }

        public static void HandleException(string errorMessage, Exception ex) {
            string debugMessage = ex.ToString();
            InternalErrorHandler(errorMessage, debugMessage);
        }
    }