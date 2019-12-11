# Injector

Commandline tool that injects one or more DLLs into a process. Optionally also starts that process before injecting.

## Features

  * Completely command line driven. No GUI
  * Most command line options supported in config file
  * Can inject multiple dlls with one call
  * Can be specified to start the process before injecting (Supports Microsoft Store apps too)
  * Delays can be adjusted to provide stable injections

## Commandline Options

  All options are optional except otherwise specified. 
  * `-p | --pid` 
  The process id of the process to inject into. `process` and `open` are ignored if this is used
  * `-x | --process` 
  The process name of the process to inject into. Alternatively, can be the name of the true process after using `--start`
  * `-s | --start` 
  The path to process to start. Supports Microsoft store apps (see below)
  * `-w | --win` 
  If passed, then the process to start is a Microsoft store app (Default: `false`)
  * `-d | --delay` 
  Specifies the delay, in seconds, after the process has started before the injection process starts (Default: `5`)
  * `-m | --multi-dll-delay` 
  Specifies the delay, in seconds, betweeen injecting multiple delays (Default: `1`)
  * `-t | --timeout` 
  Specifies the timeout, in seconds, when attempting to find the process by name (Default: `10`)
  * `-c | --config` 
  Path to the config file. Options defined in the config file override command line args
  * `-q | --quiet`
  If passed, then only errors are printed to the console. Everything is still printed to a log file (Default: `false`)
  * `-i | --interactive` 
  If passed, then program will pause before exiting, including errors (Default: `false`)
  * `-l | --dlls` 
  One or more dlls to inject into the process, separated by space. Full paths can be used or config keys (see below)

## Microsoft Store Apps

To start a Microsoft Store App, you need to use a specific format. Follow the instructions on the microsoft community site [here](https://answers.microsoft.com/en-us/windows/forum/windows_10-windows_store/starting-windows-10-store-app-from-the-command/836354c5-b5af-4d6c-b414-80e40ed14675?auth=1).

Use the `PackageFamilyName!Applicationid`. Don't include the `explorer.exe shell:appsFolder\`. So from the article, you would pass `Microsoft.BingWeather_8wekyb3d8bbwe!App`.

**Note** Don't forgot to specify the process is a Microsoft store app by using `-w | --win` or `win=true` in the config file

## Config File

All options can be defined in easy to use config files. Allows you to easily switch between configs. Values in the file override does in the command line arguments.

The config file is just a basic INI file.

```
[Config]
pid=1234
process=program.exe
start=launcher.exe
win=false
delay=5
multi-dll-delay=1
timeout=3
quiet=false
interactive=false
dlls=some_key another_key path/to/dll

[DLL]
some_key=path/to/dll
another_key=path/to/dll
```

You can specify easy to use keys instead of the the full paths to the DLLs when using the command line or config file.  You can mix keys and paths, as well.

## Troubleshooting

**It keeps crashing!**

It could you need to adjust delays. Try and use the injector after starting the program manually and waiting till you feel the program is in a state that it has fully initialized (Note: the injector is smart enough to not start the process, if specified, when it's already running).

If the injector works, then the delays. The most effective one is `--delay`
If the injector doesn't not work, sometimes it helps to adjust the order of the injection, if doing more than one dll.

Unfortunately, if the injection still fails, but works with other injection utilities, then open an issue and I'll try and take a look at it.

**I specified a Microsoft store app, but it doesn't start**

Make sure the use the format specified above and make sure to use the `win` option.


