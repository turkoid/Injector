using System;
using System.Collections.Generic;
using System.IO;
using CommandLine;
using IniParser;
using IniParser.Model;

namespace Injector {
    public class InjectorOptions {
        private static Logger logger = Logger.Instance();

        [Option('p', "pid", HelpText = "The process id of the process to inject into")]
        public int? ProcessId { get; set; }

        [Option('x', "process", HelpText = "The process name of the process to inject into")]
        public string ProcessName { get; set; }

        [Option('o', "open", HelpText = "Path to process to start")]
        public string StartProcess { get; set; }

        [Option('w', "win", Default = false, HelpText = "Indicates the process to open is a windows app")]
        public bool IsWindowsApp { get; set; }

        [Option('d', "delay", Default = 5, HelpText = "How long to wait, in seconds, before injecting")]
        public int InjectionDelay { get; set; }

        [Option('m', "multi-dll-delay", Default = 1,
            HelpText = "How long to wait, in seconds, between injecting multiple DLLs")]
        public int InjectLoopDelay { get; set; }

        [Option('t', "timeout", Default = 10,
            HelpText = "How long to wait, in seconds, when finding process by name")]
        public int Timeout { get; set; }

        [Value(0, MetaName = "DLLs",
            HelpText = "The paths or config keys to the DLLs to inject. Order determines injection order")]
        public IEnumerable<string> Dlls { get; set; }

        [Option('c', "config", HelpText = "Path to config file to use")]
        public string ConfigFile { get; set; }

        [Option('q', "quiet", Default = false, HelpText = "Don't print messages to console")]
        public bool Quiet { get; set; }

        [Option('n', "non-interactive", Default = false, HelpText = "Don't pause after errors")]
        public bool IsNonInteractive { get; set; }

        private IniData _Config { get; set; }

        public IniData Config {
            get {
                if (this._Config == null && this.ConfigFile != null) {
                    var parser = new FileIniDataParser();
                    logger.Debug($"Reading from config file {this.ConfigFile}");
                    this._Config = parser.ReadFile(this.ConfigFile);
                }

                return this._Config;
            }
        }

        private dynamic ParseConfigValue(string key, Type type, dynamic defaultValue) {
            if (this.Config.TryGetKey($"Config.{key}", out string value)) {
                logger.Debug($"Using config value for {key}");
                value = value.Trim();
                value = value == "" ? null : value;
                return Convert.ChangeType(value, type);
            }

            return defaultValue;
        }

        public void UpdateFromFile() {
            if (this.Config != null) {
                logger.Debug("Overriding command line args with config values");
                this.ProcessId = ParseConfigValue("pid", typeof(int), this.ProcessId);
                this.ProcessName = ParseConfigValue("process", typeof(string), this.ProcessName);
                this.StartProcess = ParseConfigValue("open", typeof(string), this.StartProcess);
                this.IsWindowsApp = ParseConfigValue("win", typeof(bool), this.IsWindowsApp);
                this.InjectionDelay = ParseConfigValue("delay", typeof(int), this.InjectionDelay);
                this.InjectLoopDelay = ParseConfigValue("dll_delay", typeof(int), this.InjectLoopDelay);
                this.Timeout = ParseConfigValue("timeout", typeof(int), this.Timeout);
                this.Quiet = ParseConfigValue("quiet", typeof(bool), this.Quiet);
            }
        }

        public FileInfo Dll(string key) {
            string filePath = null;
            if (this.Config != null) {
                filePath = this.Config.GetKey($"DLL.{key}")?.Trim();
            }

            return new FileInfo(string.IsNullOrEmpty(filePath) ? key : filePath);
        }

        public void Validate() {
            if (this.InjectionDelay < 0) {
                Program.HandleError("-d/--delay cannot be negative");
            }

            if (this.InjectLoopDelay < 0) {
                Program.HandleError("-i/--multi-dll-delay cannot be negative");
            }

            if (this.Timeout < 0) {
                Program.HandleError("-t/--timeout cannot be negative");
            }

            if (this.InjectionDelay == 0) {
                logger.Warn("No delay specified before attempting to inject. This could cause it to crash");
            }

            if (this.InjectLoopDelay == 0) {
                logger.Warn("No delay between injecting multiple delays. This could cause it to crash");
            }
        }
    }
}