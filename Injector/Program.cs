using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using CommandLine;
using CommandLine.Text;

namespace Injector
{
    internal class Program
    {
        private static readonly Logger logger = Logger.Instance();
        private static ParserResult<InjectorOptions> parserResult;
        public static InjectorOptions Options { get; set; }

        private static void Main(string[] args)
        {
            // dotnet publish -r win-x64 -c Release /p:PublishSingleFile=true
            try
            {
                var parser = new Parser(with => with.HelpWriter = null);
                parserResult = parser.ParseArguments<InjectorOptions>(args);
                parserResult
                    .WithParsed(
                        opts =>
                        {
                            Options = opts;
                            logger.Debug($"args: {string.Join(' ', args)}");
                            opts.UpdateFromFile();
                            opts.Log();
                            opts.Validate();

                            var injector = new Injector(opts);
                            injector.Inject();
                        }
                    )
                    .WithNotParsed(errs => DisplayHelp(parserResult, errs));
            }
            catch (Exception ex)
            {
                HandleException("An unknown error occurred. See log for details", ex);
            }

            WaitForUserInput();
        }

        public static void DisplayHelp(ParserResult<InjectorOptions> result, IEnumerable<Error> errs)
        {
            HelpText helpText = null;
            if (errs.IsVersion())
            {
                helpText = HelpText.AutoBuild(result);
            }
            else
            {
                helpText = HelpText.AutoBuild(
                    result,
                    help =>
                    {
                        help.AdditionalNewLineAfterOption = false;
                        help.Copyright = "";
                        return HelpText.DefaultParsingErrorsHandler(result, help);
                    },
                    e => e
                );
            }

            if (errs.IsVersion() || errs.IsHelp())
            {
                Console.WriteLine(helpText);
            }
            else
            {
                Console.Error.WriteLine(helpText);
            }
        }

        public static void WaitForUserInput(bool isError = false)
        {
            var interactive = Options?.Interactive ?? false;
            var noPauseOnError = Options?.NoPauseOnError ?? false;
            if (interactive || isError && !noPauseOnError)
            {
                Console.WriteLine("Press any key to exit...");
                Console.ReadKey(true);
            }
        }

        private static void InternalErrorHandler(string errorMessage, string debugMessage = null)
        {
            logger.Error(errorMessage);
            if (!string.IsNullOrEmpty(debugMessage))
            {
                logger.Debug(debugMessage);
            }

            logger.Info("See log for more details");
            WaitForUserInput(true);
            logger.Debug("Exiting...");
            Environment.Exit(1);
        }

        public static void HandleWin32Error(string errorMessage)
        {
            var exception = new Win32Exception(Marshal.GetLastWin32Error());
            var debugMessage = $"Code: {exception.ErrorCode}, Message: {exception.Message}";
            InternalErrorHandler(errorMessage, debugMessage);
        }

        public static void HandleError(string errorMessage)
        {
            InternalErrorHandler(errorMessage);
        }

        public static void HandleException(string errorMessage, Exception ex)
        {
            var debugMessage = ex.ToString();
            InternalErrorHandler(errorMessage, debugMessage);
        }
    }
}