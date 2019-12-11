using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Reflection.Metadata;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using CommandLine;
using IniParser;
using IniParser.Model;

namespace Injector {
    class Program {
        public const string LOG_FILENAME = "injector.log";
        public static Logger logger;

        static void Main(string[] args) {
            // dotnet publish -r win-x64 -c Release /p:PublishSingleFile=true
            logger = new Logger(LOG_FILENAME);
            try {
                Injector injector = new Injector();
                Parser.Default.ParseArguments<Options>(args)
                    .WithParsed(opts => {
                        logger.Quiet = opts.Quiet;
                        opts.UpdateFromFile();
                        Process process = null;
                        if (opts.ProcessId != null) {
                            // find process by pid
                            int pid = opts.ProcessId.Value;
                            try {
                                logger.Info($"Attempting to find running process by id: {opts.ProcessId}...");
                                process = Process.GetProcessById(opts.ProcessId.Value);
                            } catch (ArgumentException) {
                                HandleError($"Could not find pid: {pid}.");
                            }
                        } else if (opts.StartProcess != null) {
                            logger.Debug("Checking if process has already started");
                            process = FindProcessByName(opts.ProcessName == null
                                ? opts.StartProcess
                                : opts.ProcessName);
                            if (process == null) {
                                // starts a process
                                string app = opts.StartProcess;
                                string app_args = "";
                                if (opts.IsWindowsApp) {
                                    logger.Debug("Process to start is a microsoft store app");
                                    app = "explorer.exe";
                                    app_args = $"shell:AppsFolder\\{opts.StartProcess}";
                                }

                                logger.Info($"Starting {opts.StartProcess}...");
                                process = Process.Start(app, app_args);

                                if (opts.ProcessName != null) {
                                    // if the exe to inject to is different than the one started
                                    logger.Debug("Waiting for real process to start...");
                                    process = WaitForProcess(opts.ProcessName, opts.Timeout);
                                }
                            } else {
                                logger.Info("Process already started.");
                            }
                        } else if (opts.ProcessName != null) {
                            logger.Info($"Attempting to find running process by name {opts.ProcessName}...");
                            process = WaitForProcess(opts.ProcessName, opts.Timeout);
                        }

                        if (process == null) {
                            HandleError("No process to inject.");
                        }

                        logger.Info(
                            $"Delaying injection by {opts.InjectionDelay} second(s) to allow the process to initialize fully");
                        Thread.Sleep(opts.InjectionDelay * 1000);
                        if (process.WaitForInputIdle()) {
                            List<FileInfo> dlls = new List<FileInfo>();
                            foreach (string dll in opts.Dlls) {
                                FileInfo dllInfo = opts.Dll(dll);
                                if (!File.Exists(dllInfo.FullName)) {
                                    HandleError($"DLL not found: {dllInfo.FullName}");
                                }

                                dlls.Add(dllInfo);
                            }

                            logger.Info($"Injecting {dlls.Count} DLL(s) into {process.ProcessName} ({process.Id}).");

                            Injector injector = new Injector();
                            injector.Inject(process, dlls.ToArray(), opts.InjectLoopDelay * 1000);

                            if (process.HasExited) {
                                HandleError(
                                    "Process crashed. Try changing the dll injection order or adjusting delays.");
                            }
                        }
                    });
            } catch (Exception ex) {
                HandleException("An unknown error occurred. See log for details", ex);
            }
        }

        static Process FindProcessByName(string name) {
            name = Path.GetFileNameWithoutExtension(name);
            logger.Debug($"Finding processes matching {name}");
            Process[] processes = Process.GetProcessesByName(name);
            if (processes.Length == 1) {
                logger.Debug("Found one match!");
                return processes[0];
            }

            if (processes.Length > 1) {
                HandleError($"Too many processes matching {name}.");
            }

            return null;
        }

        static Process WaitForProcess(string name, int timeout = 10) {
            Process process = null;
            timeout *= 1000;
            int polling_rate = 500;
            logger.Debug($"Waiting for process: {name}");
            while (timeout > 0) {
                process = FindProcessByName(name);
                if (process != null) {
                    logger.Debug("Process found!");
                    break;
                }

                timeout -= polling_rate;
                Thread.Sleep(polling_rate);
            }

            if (process == null) {
                HandleError($"Timed out attempting to find {name}.");
            }

            return process;
        }

        static void HandleError(string errorMessage, bool displayLastError = false) {
            logger.Error(errorMessage);
            if (displayLastError) {
                var exception = new Win32Exception(Marshal.GetLastWin32Error());
                logger.Debug($"Code: {exception.ErrorCode}, Message: {exception.Message}");
            }

            logger.Debug("Exiting...");
            Environment.Exit(1);
        }

        static void HandleException(string message, Exception ex) {
            logger.Error(message);
            logger.Debug(ex.ToString());
            logger.Debug("Exiting...");
            Environment.Exit(1);
        }

        public class Logger {
            public enum LoggingLevel { DEBUG, INFO, ERROR }

            public String LogPath { get; set; }
            public bool Quiet { get; set; }

            public Logger(string path, bool quiet = true) {
                this.LogPath = path;
                this.InternalLog(LoggingLevel.DEBUG, "Logging started", true, false);
            }

            private void InternalLog(LoggingLevel level, string message, bool quiet, bool append = true) {
                if (level == LoggingLevel.ERROR) {
                    Console.Error.WriteLine(message);
                } else if (!quiet) {
                    Console.WriteLine(message);
                }

                using (StreamWriter w = new StreamWriter(this.LogPath, append)) {
                    w.WriteLine($"{DateTime.Now.ToString("s")} | {level.ToString("g")}: {message}");
                }
            }


            public void Log(LoggingLevel level, string message, bool quiet) {
                this.InternalLog(level, message, quiet);
            }


            public void Log(LoggingLevel level, string message) {
                this.Log(level, message, this.Quiet);
            }

            public void Debug(string message) {
                this.Log(LoggingLevel.DEBUG, message);
            }

            public void Info(string message, bool quiet) {
                this.Log(LoggingLevel.INFO, message, quiet);
            }

            public void Info(string message) {
                this.Info(message, this.Quiet);
            }

            public void Error(string message) {
                this.Log(LoggingLevel.ERROR, message, true);
            }
        }


        public class Options {
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

            [Option('i', "dll_delay", Default = 1,
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

            private IniData _Config { get; set; }

            public IniData Config {
                get {
                    if (this._Config == null && this.ConfigFile != null) {
                        var parser = new FileIniDataParser();
                        logger.Debug($"Reading from config file: {this.ConfigFile}");
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
            }
        }

        public class Injector {
            const int PROCESS_CREATE_THREAD = 0x0002;
            const int PROCESS_QUERY_INFORMATION = 0x0400;
            const int PROCESS_VM_OPERATION = 0x0008;
            const int PROCESS_VM_WRITE = 0x0020;
            const int PROCESS_VM_READ = 0x0010;

            const uint MEM_COMMIT = 0x00001000;
            const uint MEM_RESERVE = 0x00002000;
            const uint MEM_RELEASE = 0x00008000;
            const uint PAGE_READWRITE = 4;

            const int OPEN_PROCESS = PROCESS_CREATE_THREAD | PROCESS_QUERY_INFORMATION | PROCESS_VM_OPERATION |
                                     PROCESS_VM_WRITE | PROCESS_VM_READ;

            const uint MEM_CREATE = MEM_COMMIT | MEM_RESERVE;

            [DllImport("kernel32.dll", SetLastError = true)]
            public static extern IntPtr OpenProcess(int dwDesiredAccess, bool bInheritHandle, int dwProcessId);

            [DllImport("kernel32.dll", SetLastError = true)]
            public static extern int CloseHandle(IntPtr hObject);

            [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
            public static extern IntPtr GetModuleHandle(string lpModuleName);

            [DllImport("kernel32", SetLastError = true, ExactSpelling = true, CharSet = CharSet.Ansi)]
            static extern IntPtr GetProcAddress(IntPtr hModule, string procName);

            [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
            static extern IntPtr VirtualAllocEx(IntPtr hProcess, IntPtr lpAddress,
                uint dwSize, uint flAllocationType, uint flProtect);

            [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
            static extern bool VirtualFreeEx(IntPtr hProcess, IntPtr lpAddress, UIntPtr dwSize, uint dwFreeType);

            [DllImport("kernel32.dll", SetLastError = true)]
            static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, uint nSize,
                out UIntPtr lpNumberOfBytesWritten);

            [DllImport("kernel32.dll", SetLastError = true)]
            static extern IntPtr CreateRemoteThread(IntPtr hProcess,
                IntPtr lpThreadAttributes, uint dwStackSize, IntPtr lpStartAddress, IntPtr lpParameter,
                uint dwCreationFlags, IntPtr lpThreadId);

            public void Inject(Process process, FileInfo[] dlls, int delay = 3000) {
                logger.Debug("Opening handle to process");
                IntPtr procHandle = OpenProcess(OPEN_PROCESS, false, process.Id);
                if (procHandle == IntPtr.Zero) {
                    HandleError(
                        $"Unable to open {process.ProcessName}. Make sure to start the tool with Administrator privileges",
                        true);
                }

                logger.Debug("Retrieving the memory address to LoadLibraryA");
                IntPtr loadLibraryAddr = GetProcAddress(GetModuleHandle("kernel32.dll"), "LoadLibraryA");
                if (loadLibraryAddr == IntPtr.Zero) {
                    HandleError("Unable not retrieve the address for LoadLibraryA.", true);
                }

                int dllIndex = 1;
                foreach (FileInfo dll in dlls) {
                    logger.Info($"Attempting to inject DLL, {dllIndex} of {dlls.Length}: {dll.Name}...");
                    uint size = (uint)(dll.FullName.Length + 1);
                    logger.Debug("Allocating memory in the process to write the DLL path");
                    IntPtr allocMemAddress = VirtualAllocEx(procHandle, IntPtr.Zero, size, MEM_CREATE, PAGE_READWRITE);
                    if (allocMemAddress == IntPtr.Zero) {
                        HandleError(
                            "Unable to allocate memory in the process. Make sure to start the tool with Administrator privileges",
                            true);
                    }

                    logger.Debug("Writing the DLL path in the process memory");
                    bool result = WriteProcessMemory(procHandle, allocMemAddress,
                        Encoding.Default.GetBytes(dll.FullName),
                        size, out UIntPtr bytesWritten);
                    if (!result) {
                        HandleError("Failed to write the DLL path into the memory of the process", true);
                    }

                    logger.Debug("Creating remote thread. This is where the magic happens!");
                    IntPtr threadHandle = CreateRemoteThread(procHandle, IntPtr.Zero, 0, loadLibraryAddr,
                        allocMemAddress,
                        0, IntPtr.Zero);
                    if (procHandle == IntPtr.Zero) {
                        HandleError("Unable to create a remote thread in the process. Failed to inject the dll", true);
                    }

                    if (process.WaitForInputIdle()) {
                        logger.Debug("Closing remote thread");
                        CloseHandle(threadHandle);
                        logger.Debug("Freeing memory");
                        VirtualFreeEx(procHandle, allocMemAddress, UIntPtr.Zero, MEM_RELEASE);
                    }

                    if (dllIndex < dlls.Length) {
                        Thread.Sleep(delay);
                    }

                    dllIndex++;

                    logger.Info("Injected!");
                }

                logger.Debug("Closing handle to process");
                CloseHandle(procHandle);
            }
        }
    }
}