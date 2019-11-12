# Radeon Asm Debugger Extension for Visual Studio

## Syntax Highlighting

See [Radeon Asm Syntax Highlight Extension](VSRAD.Syntax).

## Installation

1. Download the installation archive attached to the latest git tag and unpack it in a temporary directory.
2. Install `RadeonAsmDebugger.vsix`.
3. Run the `install.bat` script.
4. Transfer `DebugServerW64`/`DebugServerLinux64` to your remote machines.

## Usage

* [Kernel Debug Example](Example)
* [User Manual](MANUAL.md) (WIP)

### Debug Server

Start the debug server on a remote machine with

```shell
./RadeonAsmDebugServer port
```

where `port` is a TCP port number the server will listen on.

## Development

### Prerequisites

* [.NET Core 2.2 SDK](https://dotnet.microsoft.com/download/dotnet-core/2.2)
* [.NET Framework 4.8 Developer Pack](https://dotnet.microsoft.com/download/dotnet-framework/net48)
* Visual Studio 2017/2019 with *.NET desktop development* and *Visual Studio extension development* workloads and *Modeling SDK* (available in the *Individual components* tab in Visual Studio Installer)

### Debugging

1. Clone this repository and open the solution in Visual Studio.
2. Right-click on `VSRAD.Package` in *Solution Explorer* and select *Set as StartUp Project*.
3. Right-click on `VSRAD.Package` in *Solution Explorer* and
select *Properties*.
4. Navigate to the *Debug* tab in the project properties editor.
5. Choose *Start external program* as the *Start action* and enter the path to your Visual Studio executable (`<Visual Studio installation path>\Common7\IDE\devenv.exe`).
6. Enter `/RootSuffix Exp` in the *Command line arguments* field.
7. Close the project properties editor and start debugging by pressing F5.

### Assembling a Release

1. Right-click on `VSRAD.Build` in *Solution Explorer* and select *Set as StartUp Project*.
2. Select *Release* in the *Solution Configurations* dropdown.
3. Build the entire solution (*Build* -> *Build Solution*).

The release build creates a directory (`Release`) with the installation script
(`install.bat`), the extension package (`RadeonAsmDebugger.vsix`), and debug server
binaries for Windows and Linux.
