using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CommandLine;
using CommandLine.Text;
using IniParser;
using IniParser.Model;

namespace Injector
{
    public class InjectorOptions
    {
        public const uint DEFAULT_INJECTION_DELAY = 5000;
        public const uint DEFAULT_INJECTION_LOOP_DELAY = 1000;
        public const uint DEFAULT_TIMEOUT = 30000;
        private static readonly Logger logger = Logger.Instance();

        [Option('p', "pid", HelpText = "The process id of the process to inject into")]
        public uint? ProcessId { get; set; }

        [Option('x', "process", HelpText = "The process name of the process to inject into")]
        public string ProcessName { get; set; }

        [Option('s', "start", HelpText = "Path to the process to start")]
        public string StartProcess { get; set; }

        [Option("process-restarts", Default = false, HelpText = "Indicates the process restarts")]
        public bool ProcessRestarts { get; set; }

        [Option('w', "win", Default = false, HelpText = "Indicates the process to start is a windows app")]
        public bool IsWindowsApp { get; set; }

        [Option('d', "delay", Default = DEFAULT_INJECTION_DELAY, HelpText = "Delay(ms) after starting process to start injection")]
        public uint InjectionDelay { get; set; }

        [Option("wait-for-dlls", HelpText = "The paths or config keys of DLLs the injector should wait to be loaded before attempting to inject")]
        public IEnumerable<string> WaitDlls { get; set; }

        [Option(
            'm',
            "multi-dll-delay",
            Default = DEFAULT_INJECTION_LOOP_DELAY,
            HelpText = "Delay(ms) between injecting multiple DLLs"
        )]
        public uint InjectLoopDelay { get; set; }

        [Option(
            't',
            "timeout",
            Default = DEFAULT_TIMEOUT,
            HelpText = "Timeout(ms) when finding process by name"
        )]
        public uint Timeout { get; set; }

        [Option('c', "config", HelpText = "Path to config file to use")]
        public string ConfigFile { get; set; }

        [Option('q', "quiet", Default = false, HelpText = "Suppress console messages. Errors are still printed to the console")]
        public bool Quiet { get; set; }

        [Option('v', "verbose", Default = false, HelpText = "All messages including debug messages are printed to the console")]
        public bool Verbose { get; set; }

        [Option('i', "interactive", Default = false, HelpText = "Whether to wait for user input")]
        public bool Interactive { get; set; }

        [Option('e', "no-pause-on-error", Default = false, HelpText = "Whether to not pause on errors")]
        public bool NoPauseOnError { get; set; }

        [Value(
            0,
            MetaName = "DLLs",
            HelpText = "The paths or config keys to the DLLs to inject. Order determines injection order. Use 'dlls' for the config file"
        )]
        public IEnumerable<string> Dlls { get; set; }

        [Usage(ApplicationAlias = "injector")]
        public static IEnumerable<Example> Examples
        {
            get
            {
                var shortOptions = new UnParserSettings();
                shortOptions.PreferShortName = true;
                return new List<Example>
                {
                    new Example("Inject a dll into a process matching pid", new InjectorOptions {ProcessId = 1234, Dlls = new List<string> {"path\\to\\some.dll"}}),
                    new Example(
                        "Start a process and inject multiple DLLs",
                        shortOptions,
                        new InjectorOptions {StartProcess = "process.exe", Dlls = new List<string> {"path\\to\\some.dll", "path\\to\\another.dll"}}
                    ),
                    new Example(
                        "Start a Microsoft store app and use config file keys for the DLLs",
                        shortOptions,
                        new InjectorOptions
                        {
                            StartProcess = "Microsoft!App",
                            IsWindowsApp = true,
                            ProcessName = "process.exe",
                            ConfigFile = "config.ini",
                            Dlls = new List<string> {"key1", "key2"}
                        }
                    )
                };
            }
        }

        private IniData _Config { get; set; }

        public IniData Config
        {
            get
            {
                if (_Config == null)
                {
                    if (ConfigFile == null)
                    {
                        ConfigFile = "config.ini";
                        if (!File.Exists("config.ini"))
                        {
                            logger.Debug("config.ini file not found, trying config/*.ini");
                            try
                            {
                                ConfigFile = Directory.GetFiles("config", "*.ini").FirstOrDefault();
                            }
                            catch (DirectoryNotFoundException ex)
                            {
                                logger.Debug("No config folder found");
                            }
                        }
                    }

                    if (string.IsNullOrWhiteSpace(ConfigFile))
                    {
                        Program.HandleError("No config file specified/found");
                    }
                    else
                    {
                        var configFileInfo = new FileInfo(ConfigFile);
                        var parser = new FileIniDataParser();
                        logger.Info($"Reading from config file: {ConfigFile}");
                        if (!File.Exists(configFileInfo.FullName))
                        {
                            Program.HandleError($"Config file not found: {configFileInfo.FullName}");
                        }

                        try
                        {
                            _Config = parser.ReadFile(ConfigFile);
                        }
                        catch (Exception ex)
                        {
                            Program.HandleException("There was an issue parsing the config file!", ex);
                        }
                    }
                }

                return _Config;
            }
        }

        private dynamic ParseConfigValue(string key, Type type, dynamic defaultValue)
        {
            if (Config.TryGetKey($"Config.{key}", out var value))
            {
                logger.Debug($"Using config value for {key}");
                value = value.Trim();
                if (!value.Equals(""))
                {
                    if (type == typeof(List<>))
                    {
                        var values = value.Split(' ').ToList();
                        return values;
                    }

                    return Convert.ChangeType(value, type);
                }
            }

            return defaultValue;
        }

        public void UpdateFromFile()
        {
            if (Config != null)
            {
                logger.Debug("Overriding command line args with config values");
                ProcessId = ParseConfigValue("pid", typeof(uint), ProcessId);
                ProcessName = ParseConfigValue("process", typeof(string), ProcessName);
                StartProcess = ParseConfigValue("start", typeof(string), StartProcess);
                ProcessRestarts = ParseConfigValue("process-restarts", typeof(bool), ProcessRestarts);
                IsWindowsApp = ParseConfigValue("win", typeof(bool), IsWindowsApp);
                InjectionDelay = ParseConfigValue("delay", typeof(uint), InjectionDelay);
                WaitDlls = ParseConfigValue("wait-for-dlls", typeof(List<>), WaitDlls);
                InjectLoopDelay = ParseConfigValue("multi-dll-delay", typeof(uint), InjectLoopDelay);
                Timeout = ParseConfigValue("timeout", typeof(uint), Timeout);
                Quiet = ParseConfigValue("quiet", typeof(bool), Quiet);
                Verbose = ParseConfigValue("verbose", typeof(bool), Verbose);
                Interactive = ParseConfigValue("interactive", typeof(bool), Verbose);
                NoPauseOnError = ParseConfigValue("no-pause-on-error", typeof(bool), Verbose);
                Dlls = ParseConfigValue("dlls", typeof(List<>), Dlls);
            }
        }

        public FileInfo GetDllInfo(string key)
        {
            string filePath = null;
            if (Config != null)
            {
                filePath = Config.GetKey($"DLL.{key}")?.Trim();
            }

            return new FileInfo(string.IsNullOrEmpty(filePath) ? key : filePath);
        }

        public void Validate()
        {
            if (InjectionDelay == 0)
            {
                logger.Warn("No delay specified before attempting to inject. The process could crash");
            }

            if (InjectLoopDelay == 0)
            {
                logger.Warn("No delay between injecting multiple DLLs. The process could crash");
            }

            if (ProcessId != null && (ProcessName != null || StartProcess != null))
            {
                Program.HandleError("--pid and --process/--start cannot be combined");
            }

            if (Quiet && Verbose)
            {
                Program.HandleError("--quiet and --verbose cannot be combined");
            }

            if (Interactive && NoPauseOnError)
            {
                Program.HandleError("--interactive and --no-pause-on-error cannot be combined");
            }

            if ((Dlls?.Count() ?? 0) == 0)
            {
                Program.HandleError("No DLLs to inject");
            }

            foreach (var dll in Dlls)
            {
                var dllInfo = GetDllInfo(dll);
                if (!File.Exists(dllInfo.FullName))
                {
                    Program.HandleError($"DLL not found: {dllInfo.FullName}");
                }
            }

            var existingWaitDlls = new List<string>();
            foreach (var dll in WaitDlls)
            {
                var dllInfo = GetDllInfo(dll);
                if (File.Exists(dllInfo.FullName))
                {
                    existingWaitDlls.Add(dll);
                }
                else
                {
                    logger.Warn($"Wait DLL not found: {dllInfo.FullName}");
                }
            }

            WaitDlls = existingWaitDlls;
        }

        public void Log()
        {
            var sb = new StringBuilder();
            sb.AppendLine("Injector Options:");
            sb.AppendLine($"  pid={ProcessId}");
            sb.AppendLine($"  process={ProcessName}");
            sb.AppendLine($"  start={StartProcess}");
            sb.AppendLine($"  process-restarts={ProcessRestarts}");
            sb.AppendLine($"  win={IsWindowsApp}");
            sb.AppendLine($"  delay={InjectionDelay}");
            sb.AppendLine($"  wait-for-dlls={string.Join(' ', WaitDlls)}");
            sb.AppendLine($"  multi-dll-delay={InjectLoopDelay}");
            sb.AppendLine($"  timeout={Timeout}");
            sb.AppendLine($"  quiet={Quiet}");
            sb.AppendLine($"  verbose={Verbose}");
            sb.AppendLine($"  interactive={Interactive}");
            sb.AppendLine($"  no-pause-on-error={NoPauseOnError}");
            sb.AppendLine($"  dlls={string.Join(' ', Dlls)}");
            logger.Debug(sb.ToString());
        }
    }
}