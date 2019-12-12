using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CommandLine;
using CommandLine.Text;
using IniParser;
using IniParser.Model;

namespace Injector {
    public class InjectorOptions {
        private static readonly Logger logger = Logger.Instance();

        [Option('p', "pid", HelpText = "The process id of the process to inject into")]
        public int? ProcessId { get; set; }

        [Option('x', "process", HelpText = "The process name of the process to inject into")]
        public string ProcessName { get; set; }

        [Option('s', "start", HelpText = "Path to the process to start")]
        public string StartProcess { get; set; }

        [Option('w', "win", Default = false, HelpText = "Indicates the process to start is a windows app")]
        public bool IsWindowsApp { get; set; }

        [Option('d', "delay", Default = 5, HelpText = "Delay, in seconds, after starting process to start injection")]
        public int InjectionDelay { get; set; }

        [Option('m', "multi-dll-delay", Default = 1,
            HelpText = "Delay, in seconds, between injecting multiple DLLs")]
        public int InjectLoopDelay { get; set; }

        [Option('t', "timeout", Default = 10,
            HelpText = "Timeout, in seconds, when finding process by name")]
        public int Timeout { get; set; }

        [Option('c', "config", HelpText = "Path to config file to use")]
        public string ConfigFile { get; set; }

        [Option('q', "quiet", Default = false, HelpText = "Suppress console messages. Errors are still printed to the console")]
        public bool Quiet { get; set; }

        [Option('v', "verbose", Default = false, HelpText = "All messages including debug messages are printed to the console")]
        public bool Verbose { get; set; }

        [Option('i', "interactive", Default = false, HelpText = "Whether to wait for user input")]
        public bool Interactive { get; set; }

        [Value(0, MetaName = "DLLs",
            HelpText = "The paths or config keys to the DLLs to inject. Order determines injection order. Use 'dlls' for the config file")]
        public IEnumerable<string> Dlls { get; set; }

        [Usage(ApplicationAlias = "injector")]
        public static IEnumerable<Example> Examples {
            get {
                UnParserSettings shortOptions = new UnParserSettings();
                shortOptions.PreferShortName = true;
                return new List<Example> {
                    new Example("Inject a dll into a process matching pid", new InjectorOptions {ProcessId = 1234, Dlls = new List<string> {"path\\to\\some.dll"}}),
                    new Example("Start a process and inject multiple DLLs", shortOptions,
                        new InjectorOptions {StartProcess = "process.exe", Dlls = new List<string> {"path\\to\\some.dll", "path\\to\\another.dll"}}),
                    new Example("Start a Microsoft store app and use config file keys for the DLLs", shortOptions,
                        new InjectorOptions {
                            StartProcess = "Microsoft!App",
                            IsWindowsApp = true,
                            ProcessName = "process.exe",
                            ConfigFile = "config.ini",
                            Dlls = new List<string> {"key1", "key2"}
                        })
                };
            }
        }

        private IniData _Config { get; set; }

        public IniData Config {
            get {
                if (_Config == null && !string.IsNullOrWhiteSpace(ConfigFile)) {
                    var parser = new FileIniDataParser();
                    FileInfo configFileInfo = new FileInfo(ConfigFile);
                    logger.Info($"Reading from config file: {ConfigFile}");
                    if (!File.Exists(configFileInfo.FullName)) {
                        Program.HandleError($"Config file not found: {configFileInfo.FullName}");
                    }

                    try {
                        _Config = parser.ReadFile(ConfigFile);
                    } catch (Exception ex) {
                        Program.HandleException("There was an issue parsing the config file!", ex);
                    }
                }

                return _Config;
            }
        }

        private dynamic ParseConfigValue(string key, Type type, dynamic defaultValue) {
            if (Config.TryGetKey($"Config.{key}", out string value)) {
                logger.Debug($"Using config value for {key}");
                value = value.Trim();
                value = value == "" ? null : value;
                if (type == typeof(List<>)) {
                    List<string> values = value.Split(' ').ToList();
                    return values;
                }

                return Convert.ChangeType(value, type);
            }

            return defaultValue;
        }

        public void UpdateFromFile() {
            if (Config != null) {
                logger.Debug("Overriding command line args with config values");
                ProcessId = ParseConfigValue("pid", typeof(int), ProcessId);
                ProcessName = ParseConfigValue("process", typeof(string), ProcessName);
                StartProcess = ParseConfigValue("start", typeof(string), StartProcess);
                IsWindowsApp = ParseConfigValue("win", typeof(bool), IsWindowsApp);
                InjectionDelay = ParseConfigValue("delay", typeof(int), InjectionDelay);
                InjectLoopDelay = ParseConfigValue("multi-dll-delay", typeof(int), InjectLoopDelay);
                Timeout = ParseConfigValue("timeout", typeof(int), Timeout);
                Quiet = ParseConfigValue("quiet", typeof(bool), Quiet);
                Verbose = ParseConfigValue("verbose", typeof(bool), Verbose);
                Dlls = ParseConfigValue("dlls", typeof(List<>), Dlls);
            }
        }

        public FileInfo Dll(string key) {
            string filePath = null;
            if (Config != null) {
                filePath = Config.GetKey($"DLL.{key}")?.Trim();
            }

            return new FileInfo(string.IsNullOrEmpty(filePath) ? key : filePath);
        }

        public void Validate() {
            if (InjectionDelay < 0) {
                Program.HandleError("'delay' cannot be negative");
            }

            if (InjectLoopDelay < 0) {
                Program.HandleError("'multi-dll-delay' cannot be negative");
            }

            if (Timeout < 0) {
                Program.HandleError("'timeout' cannot be negative");
            }

            if (InjectionDelay == 0) {
                logger.Warn("No delay specified before attempting to inject. The process could crash");
            }

            if (InjectLoopDelay == 0) {
                logger.Warn("No delay between injecting multiple DLLs. The process could crash");
            }

            if (Quiet && Verbose) {
                Program.HandleError("'quiet' and 'verbose' cannot be combined");
            }

            if ((Dlls?.Count() ?? 0) == 0) {
                Program.HandleError("No DLLs to inject");
            }
        }

        public void Log() {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Injector Options:");
            sb.AppendLine($"  pid={ProcessId}");
            sb.AppendLine($"  process={ProcessName}");
            sb.AppendLine($"  start={StartProcess}");
            sb.AppendLine($"  win={IsWindowsApp}");
            sb.AppendLine($"  delay={InjectionDelay}");
            sb.AppendLine($"  multi-dll-delay={InjectLoopDelay}");
            sb.AppendLine($"  timeout={Timeout}");
            sb.AppendLine($"  quiet={Quiet}");
            sb.AppendLine($"  interactive={Interactive}");
            sb.AppendLine($"  verbose={Verbose}");
            sb.AppendLine($"  dlls={string.Join(' ', Dlls)}");
            logger.Debug(sb.ToString());
        }

        public struct CommandLineOption {
            public char ShortOption;
            public string LongOption;

            public CommandLineOption(char shortOption, string longOption) {
                ShortOption = shortOption;
                LongOption = longOption;
            }
        }
    }
}