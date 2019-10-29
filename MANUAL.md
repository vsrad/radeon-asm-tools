# Radeon Asm Debugger Extension for Visual Studio User Manual

The extension provides the following features:

* [Project template](#project-template)
* [Profiles](#profiles)
* Remote debugging tools
* Disassembling tools
* Profiling tools
* Data visualization tools

## Project Template

To use the tools described below, you must first create a new
`Radeon Asm Project`.

After [installing the extension](README.md#installation), you should see
the project template in the *Create a new project* dialog:

 ![Template](Resources/Manual/ProjTemplate.PNG)

You can find it by selecting `Radeon Asm` in the *Languages* dropdown.

## Profiles

A *profile* is a set of machine-specific settings such as executable paths, network addresses, and debugger configuration.

Multiple profiles can be created for a single project and swapped without restarting the IDE or exiting debug mode. This allows you to easily test the same kernel on a number of different platforms. Furthermore, profiles can be exported to a file and imported in other projects (on different workstations).

The following sections provide an overview of the properties that can be configured in a single profile:

* [General](#general-properties)
* [Debugger](#debugger-properties)
* Disassembler
* Profiler

Other chapters will reference these sections as needed.

### General Properties

* **Profile Name**: name of the current profile.
* **Deploy Directory**: directory on the remote machine where the project is deployed before starting the debugger.
* **Remote Machine Address**: IP address of the remote machine. To debug kernels locally, start the debug server on your local machine and enter `127.0.0.1` in this field.
* **Port**: port on the remote machine the debug server is listening on. (When started without arguments, the server listens on port `9339`)
* **Autosave Source**: specifies whether the source files that are changed should be automatically saved before running remote commands (debug, disassemble, profile, etc.).
  - When set to **None**, no source files are saved automatically.
  - When set to **ActiveDocument**, only the active source file (currently selected in the editor) is automatically saved.
  - When set to **ActiveDocuments**, all source files the are open in the editor are automatically saved.
  - When set to **ProjectDocuments**, all source files that belong to the current project are automatically saved.
  - When set to **SolutionDocuments**, all source file that belong to the solution are automatically saved.
* **Additional Sources**: a semicolon-separated list of out-of-project paths that are copied to the remote machine (see **Deploy Directory**).
* **Copy Sources to Remote**: enables or disables remote deployment.

### Debugger Properties

* **Executable**: path to the debugger executable on the remote machine.
* **Arguments**: command-line arguments for **Executable**.
* **Working Directory**: debugger working directory.
* **Output Path**: path to the debug script output file (can be relative to **Working Directory**).
* **Output Mode**: specifies how the debug script output file is parsed:
  - **Text**: the first line is skipped, and each succeeding line is read as a hexadecimal string (*0x...*).
  - **Binary**: 4-byte blocks are read as a single dword value.
* **Run As Administrator**: specifies whether the `Executable` is run with administrator rights.
* **Timeout**: debug script execution timeout (in seconds). Once reached, the debug process is terminated. Set to `0` to disable.
* **Parse Valid Watches File**: specifies whether the file specified in **Valid Watches File Path** should be used to filter vald watches.
* **Valid Watches File Path**: path to the file with valid watch names on the remote machine.
