# Radeon Asm Debugger Extension for Visual Studio

## Installation

1. Download the installation archive attached to the latest git tag and unpack it in a temporary directory.
2. Install `RadeonAsmSyntax.vsix` (optionally, documentation is available [here](https://github.com/vsrad/radeon-asm-tools/tree/master/VSRAD.Syntax#visual-studio-radeon-asm-syntax-highlight-extension))
3. Install `RadeonAsmDebugger.vsix`.
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

* [.NET Core 3.1 SDK (x64)](https://dotnet.microsoft.com/download/dotnet-core/3.1)
* [.NET Framework 4.8 Developer Pack](https://dotnet.microsoft.com/download/dotnet-framework/net48)
* Visual Studio 2019 with *.NET desktop development* and *Visual Studio extension development* workloads and *Modeling SDK* (available in the *Individual components* tab in Visual Studio Installer)

### Debugging

1. Clone this repository and open the solution in Visual Studio.
2. Right-click on `VSRAD.Package` in *Solution Explorer* and select *Set as StartUp Project*.
3. Start debugging by pressing F5.

### Assembling a Release

1. Right-click on `VSRAD.Build` in *Solution Explorer* and select *Set as StartUp Project*.
2. Select *Release* in the *Solution Configurations* dropdown.
3. Build the entire solution (*Build* -> *Build Solution*).

The release build creates a directory (`Release`) with the installation script
(`install.bat`), the extension package (`RadeonAsmDebugger.vsix`), and debug server
binaries for Windows and Linux.
