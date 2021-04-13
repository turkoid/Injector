# Injector

[![Build status](https://img.shields.io/gitlab/pipeline/turkoid/Injector/master)](https://gitlab.com/turkoid/Injector/commits/master)

Commandline tool that injects one or more DLLs into a process. Optionally also starts that process before injecting.

## Download

Download latest official release [here](https://github.com/turkoid/Injector/releases/latest)

Download latest dev build [here](https://gitlab.com/turkoid/Injector/pipelines/master/latest)

## Features

  * Completely command line driven. No GUI
  * Most command-line options supported in config file
  * Can inject multiple dlls with one call
  * Can be specified to start the process before injecting (Supports Microsoft Store apps too)
  * Delays can be adjusted to provide stable injections
  * Can wait for specific dlls to be loaded before injecting
  * Smart injection delay waits for all DLLs to load

## Command-line Options

  * `-p | --pid`

    The process id of the process to inject into

  * `-x | --process`

    The process name of the process to inject into. Alternatively, can be the name of the true process after using `--start`

  * `-s | --start`

    The path to process to start. Supports Microsoft store apps (see below)

  * `--process-restarts`

    If passed, then the injector will explicily see if the process restarts itself. Explicitly passing this saves some time, but the injector can autodetect this

  * `-w | --win`

    If passed, then the process to start is a Microsoft store app (Default: `false`)

  * `-d | --delay`

    Specifies the delay, in milliseconds, after the process has started and initialization has completed, before the injection process starts (Default: `5000`)

  * `--wait-for-dlls`

    DLLs that should be loaded before attempting to inject. Multiple DLLs should be separated by space. Full paths can be used or config keys (see below)

  * `-m | --multi-dll-delay`

    Specifies the delay, in milliseconds, betweeen injecting multiple delays (Default: `1000`)

  * `-t | --timeout`

    Specifies the timeout, in milliseconds, when attempting to find the process by name (Default: `30000`)

  * `-c | --config`

    Path to the config file. Options defined in the config file override command-line args (Default: `config.ini` or `config/*.ini`)

  * `-q | --quiet`

    If passed, then only errors are printed to the console. Everything is still printed to the log file (Default: `false`)

  * `-i | --interactive`

    If passed, then the program will pause before exiting, including errors (Default: `false`)

  * `-e | --no-pause-on-error`

    If passed, then the program will not pause on errors (Default: `false`)

  * `-v | --verbose`

    If passed, then all messages, including debug messages, will be printed to the console (Default: `false`)

  * `DLLS (pos. 0)`

    DLLs to inject into the process. Multiple DLLs should be separated by space. Full paths can be used or config keys (see below)


## Microsoft Store Apps

To start a Microsoft Store App, you need to use a specific format. Follow the instructions on the microsoft community site [here](https://answers.microsoft.com/en-us/windows/forum/windows_10-windows_store/starting-windows-10-store-app-from-the-command/836354c5-b5af-4d6c-b414-80e40ed14675?auth=1).

Use the `PackageFamilyName!Applicationid`. Don't include the `explorer.exe shell:appsFolder\`. So from the article, you would pass `Microsoft.BingWeather_8wekyb3d8bbwe!App`.

The tool is smart enough to detect if you are you are trying to start a Microsoft app by detecting an exclamation point. So passing `-w | --win` or `win=true` in the config file, is not necessary. However, if for some reason you had a file named exactly like the store in the injector's working directory, then you would need to specicy the command line option.

## Config File

All options can be defined using an INI config file. Allows you to easily switch between configs. Values in the file override those passed in the command line.

```
[Config]
pid=1234
process=program.exe
start=launcher.exe
process-restarts=false
win=false
delay=5
wait-for-dlls=required_dll
multi-dll-delay=1
timeout=3
quiet=false
interactive=false
verbose=false
dlls=some_key another_key path\to\dll

[DLL]
some_key=path\to\dll
another_key=path\to\another\dll
required_dll=path\to\required\dll
```

You can specify easy to use keys instead of the the full paths to the DLLs when using the command line or config file.  You can mix keys and paths, as well.

Note: If no config file is passed as an argument, the injector will default to `config.ini` in the working directory or the first `*.ini` file located in the `config` subfolder.

## Troubleshooting

**It keeps crashing!**

You might need to adjust delays. Try and use the injector after starting the program manually and waiting till you feel the program is in a state that it has fully initialized (The injector is smart enough to not start the process, if specified, when it's already running).

If the injector works, then adjust the delays. The most effective one is `delay`

If the injector doesn't work, sometimes it helps to adjust the order of the injection, if using more than one DLL.

Unfortunately, if the injection still fails, but works with other injection utilities, then open an issue and I'll try and take a look at it.

**I specified a Microsoft store app, but it doesn't start**

Make sure the use the format specified above.

## Disclaimer

Use this tool at your own risk. I am not responsible if you inject unsafe DLLs or use this for malicious purposes. This was originally developed to make an easy to use launcher for injecting safe dlls into "The Outer Worlds" game. Any games that use anti-cheat systems, can and probably will detect this common method of injection.
