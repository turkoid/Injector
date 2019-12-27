using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace Injector {
    class Injector {
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
        private static readonly Logger logger = Logger.Instance();

        private readonly InjectorOptions opts;

        public Injector(InjectorOptions opts) {
            this.opts = opts;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern IntPtr OpenProcess(int dwDesiredAccess, bool bInheritHandle, int dwProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern int CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        static extern IntPtr GetModuleHandle(string lpModuleName);

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

        public void Inject() {
            Process process = null;
            if (opts.ProcessId != null) {
                // find process by pid
                uint pid = opts.ProcessId.Value;
                try {
                    logger.Info("Attempting to find running process by id...");
                    process = Process.GetProcessById((int)opts.ProcessId.Value);
                } catch (ArgumentException) {
                    Program.HandleError($"Could not find process id {pid}");
                }
            } else if (opts.StartProcess != null) {
                logger.Debug("Checking if process has already started");
                process = FindProcessByName(opts.ProcessName == null
                    ? opts.StartProcess
                    : opts.ProcessName);
                if (process == null) {
                    // starts a process
                    if (!File.Exists(opts.StartProcess)) {
                        if (opts.StartProcess.Contains('!')) {
                            logger.Debug("Could not find the process to start, but it could be a Microsoft store app");
                            opts.IsWindowsApp = true;
                        } else {
                            Program.HandleError($"{opts.StartProcess} not found. Ensure the path is correct");
                        }
                    }
                    string app = opts.StartProcess;
                    string app_args = "";
                    if (opts.IsWindowsApp) {
                        logger.Debug("Process to start is a Microsoft store app");
                        app = "explorer.exe";
                        app_args = $"shell:AppsFolder\\{opts.StartProcess}";
                    }

                    logger.Info($"Starting {opts.StartProcess}");
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
                logger.Info("Attempting to find running process by name...");
                process = WaitForProcess(opts.ProcessName, opts.Timeout);
            }

            if (process == null) {
                Program.HandleError("No process to inject.");
            }

            if (opts.StartProcess == null) {
                logger.Debug("Not delaying before injection. Process already started.");
            } else if (opts.InjectionDelay > 0) {
                logger.Info(
                    $"Delaying injection by {opts.InjectionDelay}ms to allow the process to initialize fully");
                Thread.Sleep((int)opts.InjectionDelay);
            }

            if (process.WaitForInputIdle()) {
                List<FileInfo> dlls = new List<FileInfo>();
                foreach (string dll in opts.Dlls) {
                    FileInfo dllInfo = opts.Dll(dll);
                    if (!File.Exists(dllInfo.FullName)) {
                        Program.HandleError($"DLL not found: {dllInfo.FullName}");
                    }

                    dlls.Add(dllInfo);
                }

                logger.Info($"Injecting {dlls.Count} DLL(s) into {process.ProcessName} ({process.Id})");
                InjectIntoProcess(process, dlls.ToArray(), opts.InjectLoopDelay);

                if (process.HasExited) {
                    Program.HandleError(
                        "Process crashed. Try changing the dll injection order or adjusting delays");
                }
            }
        }

        private void InjectIntoProcess(Process process, FileInfo[] dlls, uint delay = InjectorOptions.DEFAULT_INJECTION_LOOP_DELAY) {
            logger.Debug("Opening handle to process");
            IntPtr procHandle = OpenProcess(OPEN_PROCESS, false, process.Id);
            if (procHandle == IntPtr.Zero) {
                Program.HandleWin32Error(
                    $"Unable to open {process.ProcessName}. Make sure to start the tool with Administrator privileges");
            }

            logger.Debug("Retrieving the memory address to LoadLibraryA");
            IntPtr loadLibraryAddr = GetProcAddress(GetModuleHandle("kernel32.dll"), "LoadLibraryA");
            if (loadLibraryAddr == IntPtr.Zero) {
                Program.HandleWin32Error("Unable not retrieve the address for LoadLibraryA");
            }

            int dllIndex = 1;
            foreach (FileInfo dll in dlls) {
                logger.Info($"Attempting to inject DLL, {dllIndex} of {dlls.Length}, {dll.Name}...");
                uint size = (uint)(dll.FullName.Length + 1);
                logger.Debug("Allocating memory in the process to write the DLL path");
                IntPtr allocMemAddress = VirtualAllocEx(procHandle, IntPtr.Zero, size, MEM_CREATE, PAGE_READWRITE);
                if (allocMemAddress == IntPtr.Zero) {
                    Program.HandleWin32Error(
                        "Unable to allocate memory in the process. Make sure to start the tool with Administrator privileges");
                }

                logger.Debug("Writing the DLL path in the process memory");
                bool result = WriteProcessMemory(procHandle, allocMemAddress,
                    Encoding.Default.GetBytes(dll.FullName),
                    size, out UIntPtr bytesWritten);
                if (!result) {
                    Program.HandleWin32Error("Failed to write the DLL path into the memory of the process");
                }

                logger.Debug("Creating remote thread. This is where the magic happens!");
                IntPtr threadHandle = CreateRemoteThread(procHandle, IntPtr.Zero, 0, loadLibraryAddr,
                    allocMemAddress,
                    0, IntPtr.Zero);
                if (procHandle == IntPtr.Zero) {
                    Program.HandleWin32Error("Unable to create a remote thread in the process. Failed to inject the dll");
                }

                if (process.WaitForInputIdle()) {
                    logger.Debug("Closing remote thread");
                    CloseHandle(threadHandle);
                    logger.Debug("Freeing memory");
                    VirtualFreeEx(procHandle, allocMemAddress, UIntPtr.Zero, MEM_RELEASE);
                }

                if (dllIndex < dlls.Length) {
                    if (delay == 0) {
                        logger.Debug("No delay between next DLL injection");
                    } else {
                        logger.Debug($"Delaying next DLL injection by {delay} ms");
                        Thread.Sleep((int)delay);
                    }
                }

                dllIndex++;

                logger.Info("Injected!");
            }

            logger.Debug("Closing handle to process");
            CloseHandle(procHandle);
        }

        private Process FindProcessByName(string name) {
            name = Path.GetFileNameWithoutExtension(name);
            logger.Debug($"Finding processes matching '{name}'");
            Process[] processes = Process.GetProcessesByName(name);
            if (processes.Length == 1) {
                logger.Debug("Found one match!");
                return processes[0];
            }

            if (processes.Length > 1) {
                Program.HandleError($"Too many processes matching {name}");
            }

            logger.Debug("No process found matching the supplied name");
            return null;
        }

        private Process WaitForProcess(string name, uint timeout = InjectorOptions.DEFAULT_TIMEOUT) {
            Process process = null;
            int timeout_counter = (int)timeout;
            int polling_rate = 500;

            logger.Debug($"Waiting for process '{name}'");
            while (timeout_counter > 0) {
                process = FindProcessByName(name);
                if (process != null) {
                    logger.Debug("Process found!");
                    break;
                }

                timeout_counter -= polling_rate;
                Thread.Sleep(polling_rate);
            }

            if (process == null) {
                Program.HandleError($"Timed out waiting for process '{name}'");
            }

            return process;
        }
    }
}