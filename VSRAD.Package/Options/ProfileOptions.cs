using Newtonsoft.Json;
using System;
using System.ComponentModel;
using System.Threading.Tasks;
using VSRAD.Package.ProjectSystem;
using VSRAD.Package.ProjectSystem.Macros;

namespace VSRAD.Package.Options
{
    /// <summary>
    /// Defines the property macro name as displayed in <see cref="ProjectSystem.Profiles.ProfileOptionsWindow"/>.
    /// IMPORTANT: as of now, when defining a new macro you need to add it to <see cref="MacroEvaluator.GetMacroValueAsync"/> manually.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public sealed class MacroAttribute : Attribute
    {
        public string MacroName { get; }

        public MacroAttribute(string macroName) => MacroName = macroName;
    }

    [AttributeUsage(AttributeTargets.Property)]
    public sealed class BooleanDisplayValuesAttribute : Attribute
    {
        public string True { get; }
        public string False { get; }

        public BooleanDisplayValuesAttribute(string trueString, string falseString)
        {
            True = trueString;
            False = falseString;
        }
    }

    public sealed class ProfileOptions : ICloneable
    {
        public GeneralProfileOptions General { get; }
        public DebuggerProfileOptions Debugger { get; }
        public DisassemblerProfileOptions Disassembler { get; }
        public ProfilerProfileOptions Profiler { get; }

        public ProfileOptions(GeneralProfileOptions general = null, DebuggerProfileOptions debugger = null, DisassemblerProfileOptions disassembler = null, ProfilerProfileOptions profiler = null)
        {
            General = general ?? new GeneralProfileOptions();
            Debugger = debugger ?? new DebuggerProfileOptions();
            Disassembler = disassembler ?? new DisassemblerProfileOptions();
            Profiler = profiler ?? new ProfilerProfileOptions();
        }

        public object Clone()
        {
            ProfileOptions clonedProfile;
            try
            {
                var serializedProfile = JsonConvert.SerializeObject(this, Formatting.None);
                clonedProfile = JsonConvert.DeserializeObject<ProfileOptions>(serializedProfile);
            }
            catch (Exception e)
            {
                clonedProfile = new ProfileOptions();
                Errors.ShowWarning($"An error has occurred while cloning the profile: {e.Message} Proceeding with new profile.");
            }
            return clonedProfile;
        }
    }

    public sealed class GeneralProfileOptions
    {
        [Macro(RadMacros.DeployDirectory), DisplayName("Deploy Directory"), Description("Remote Deploy Directory")]
        [DefaultValue(DefaultOptionValues.DeployDirectory)]
        public string DeployDirectory { get; }
        [DisplayName("Remote Machine Address"), Description("Remote Machine Address")]
        [DefaultValue(DefaultOptionValues.RemoteMachineAdredd)]
        public string RemoteMachine { get; }
        [Description("Port")]
        [DefaultValue(DefaultOptionValues.Port)]
        public int Port { get; }
        [Description("Debugger autosave source"), DisplayName("Autosave Source")]
        [DefaultValue(DefaultOptionValues.AutosaveSource)]
        public DocumentSaveType AutosaveSource { get; }
        [Description("Copy files to remote machine"), DisplayName("Copy Sources to Remote")]
        [DefaultValue(DefaultOptionValues.CopySources)]
        public bool CopySources { get; }
        [Description("Semicolon-separated list of additional files/directories to copy on remote machine"), DisplayName("Additional Sources")]
        [DefaultValue(DefaultOptionValues.AdditionalSources)]
        public string AdditionalSources { get; }

        [JsonIgnore]
        public ServerConnectionOptions Connection => new ServerConnectionOptions(RemoteMachine, Port);

        public async Task<GeneralProfileOptions> EvaluateAsync(IMacroEvaluator macroEvaluator) =>
            new GeneralProfileOptions(deployDirectory: await macroEvaluator.GetMacroValueAsync(RadMacros.DeployDirectory),
                remoteMachine: RemoteMachine, port: Port, autosaveSource: AutosaveSource, copySources: CopySources, additionalSources: AdditionalSources);

        public GeneralProfileOptions(string deployDirectory = null, string remoteMachine = DefaultOptionValues.RemoteMachineAdredd, int port = DefaultOptionValues.Port, DocumentSaveType autosaveSource = DefaultOptionValues.AutosaveSource, string additionalSources = DefaultOptionValues.AdditionalSources, bool copySources = DefaultOptionValues.CopySources)
        {
            DeployDirectory = deployDirectory ?? DefaultOptionValues.DeployDirectory;
            RemoteMachine = remoteMachine;
            Port = port;
            AutosaveSource = autosaveSource;
            AdditionalSources = additionalSources;
            CopySources = copySources;
        }
    }

    public sealed class DebuggerProfileOptions
    {
        [Macro(RadMacros.DebuggerExecutable), Description("Debugger Executable")]
        [DefaultValue(DefaultOptionValues.DebuggerExecutable)]
        public string Executable { get; }
        [Macro(RadMacros.DebuggerArguments), Description("Debugger Arguments")]
        [DefaultValue(DefaultOptionValues.DebuggerArguments)]
        public string Arguments { get; }
        [Macro(RadMacros.DebuggerWorkingDirectory), Description("Debugger Working Directory"), DisplayName("Working Directory")]
        [DefaultValue(DefaultOptionValues.DebuggerWorkingDirectory)]
        public string WorkingDirectory { get; }
        [Macro(RadMacros.DebuggerOutputPath), Description("The path to the debug script output file."), DisplayName("Output Path")]
        [DefaultValue(DefaultOptionValues.DebuggerOutputPath)]
        public string OutputPath { get; }
        [Description("Debugger Output Mode"), DisplayName("Output Mode"), BooleanDisplayValues("Binary", "Text")]
        [DefaultValue(DefaultOptionValues.DebuggerBinaryOutput)]
        public bool BinaryOutput { get; }
        [Description("Parse Valid Watches File"), DisplayName("Parse Valid Watches File")]
        [DefaultValue(DefaultOptionValues.DebuggerParseValidWatches)]
        public bool ParseValidWatches { get; }
        [Description("Path to file that contains list of valid watches"), DisplayName("Valid Watchws File Path")]
        [DefaultValue(DefaultOptionValues.DebuggerValidWatchesFilePath)]
        public string ValidWatchesFilePath { get; }
        [Description("Debugger Run As Administrator"), DisplayName("Run As Administrator")]
        [DefaultValue(DefaultOptionValues.DebuggerRunAsAdmin)]
        public bool RunAsAdmin { get; }
        [Description("Debugger Timeout (seconds), 0 - timeout disabled"), DisplayName("Timeout")]
        [DefaultValue(DefaultOptionValues.DebuggerTimeoutSecs)]
        public int TimeoutSecs { get; }

        [JsonIgnore]
        public OutputFile RemoteOutputFile => new OutputFile(WorkingDirectory, OutputPath, BinaryOutput);
        [JsonIgnore]
        public OutputFile ValidWatchesFile => new OutputFile(WorkingDirectory, ValidWatchesFilePath, false);

        public async Task<DebuggerProfileOptions> EvaluateAsync(IMacroEvaluator macroEvaluator) =>
            new DebuggerProfileOptions(
                executable: await macroEvaluator.GetMacroValueAsync(RadMacros.DebuggerExecutable),
                arguments: await macroEvaluator.GetMacroValueAsync(RadMacros.DebuggerArguments),
                workingDirectory: await macroEvaluator.GetMacroValueAsync(RadMacros.DebuggerWorkingDirectory),
                outputPath: await macroEvaluator.GetMacroValueAsync(RadMacros.DebuggerOutputPath),
                binaryOutput: BinaryOutput,
                runAsAdmin: RunAsAdmin,
                timeoutSecs: TimeoutSecs,
                parseValidWatches: ParseValidWatches,
                validWatchesFilePath: ValidWatchesFilePath);

        public DebuggerProfileOptions(string executable = null, string arguments = null, string workingDirectory = DefaultOptionValues.DebuggerWorkingDirectory, string outputPath = DefaultOptionValues.DebuggerOutputPath, bool binaryOutput = DefaultOptionValues.DebuggerBinaryOutput, bool runAsAdmin = DefaultOptionValues.DebuggerRunAsAdmin, int timeoutSecs = DefaultOptionValues.DebuggerTimeoutSecs, bool parseValidWatches = DefaultOptionValues.DebuggerParseValidWatches, string validWatchesFilePath = DefaultOptionValues.DebuggerValidWatchesFilePath)
        {
            Executable = executable ?? DefaultOptionValues.DebuggerExecutable;
            Arguments = arguments ?? DefaultOptionValues.DebuggerArguments;
            WorkingDirectory = workingDirectory;
            OutputPath = outputPath;
            BinaryOutput = binaryOutput;
            RunAsAdmin = runAsAdmin;
            TimeoutSecs = timeoutSecs;
            ParseValidWatches = parseValidWatches;
            ValidWatchesFilePath = validWatchesFilePath;
        }
    }

    public sealed class DisassemblerProfileOptions
    {
        [Macro(RadMacros.DisassemblerExecutable), Description("Disassembler Executable")]
        [DefaultValue(DefaultOptionValues.DisassemblerExecutable)]
        public string Executable { get; }
        [Macro(RadMacros.DisassemblerArguments), Description("Disassembler Executable Arguments"), DisplayName("Arguments")]
        [DefaultValue(DefaultOptionValues.DisassemblerArguments)]
        public string Arguments { get; }
        [Macro(RadMacros.DisassemblerWorkingDirectory), Description("Disassembler Working Directory"), DisplayName("Working Directory")]
        [DefaultValue(DefaultOptionValues.DisassemblerWorkingDirectory)]
        public string WorkingDirectory { get; }
        [Macro(RadMacros.DisassemblerOutputPath), Description("Disassembler Output Path"), DisplayName("Output Path")]
        [DefaultValue(DefaultOptionValues.DisassemblerOutputPath)]
        public string OutputPath { get; }
        [Macro(RadMacros.DisassemblerLocalPath), Description("Disassembler Local Path"), DisplayName("Local Path")]
        [DefaultValue(DefaultOptionValues.DisassemblerLocalOutputCopyPath)]
        public string LocalOutputCopyPath { get; }
        [Description("Disassembler Line Marker"), DisplayName("Line Marker")]
        [DefaultValue(DefaultOptionValues.DisassemblerLineMaker)]
        public string LineMarker { get; }

        [JsonIgnore]
        public OutputFile RemoteOutputFile => new OutputFile(WorkingDirectory, OutputPath, binaryOutput: true);

        public async Task<DisassemblerProfileOptions> EvaluateAsync(IMacroEvaluator macroEvaluator) =>
            new DisassemblerProfileOptions(
                executable: await macroEvaluator.GetMacroValueAsync(RadMacros.DisassemblerExecutable),
                arguments: await macroEvaluator.GetMacroValueAsync(RadMacros.DisassemblerArguments),
                workingDirectory: await macroEvaluator.GetMacroValueAsync(RadMacros.DisassemblerWorkingDirectory),
                outputPath: await macroEvaluator.GetMacroValueAsync(RadMacros.DisassemblerOutputPath),
                localOutputCopyPath: await macroEvaluator.GetMacroValueAsync(RadMacros.DisassemblerLocalPath),
                lineMarker: LineMarker);

        public DisassemblerProfileOptions(string executable = DefaultOptionValues.DisassemblerExecutable, string arguments = DefaultOptionValues.DisassemblerArguments, string workingDirectory = DefaultOptionValues.DisassemblerWorkingDirectory, string outputPath = DefaultOptionValues.DisassemblerOutputPath, string localOutputCopyPath = DefaultOptionValues.DisassemblerLocalOutputCopyPath, string lineMarker = DefaultOptionValues.DisassemblerLineMaker)
        {
            Executable = executable;
            Arguments = arguments;
            WorkingDirectory = workingDirectory;
            OutputPath = outputPath;
            LocalOutputCopyPath = localOutputCopyPath;
            LineMarker = lineMarker;
        }
    }

    public sealed class ProfilerProfileOptions
    {
        [Macro(RadMacros.ProfilerExecutable), Description("Executable")]
        [DefaultValue(DefaultOptionValues.ProfilerExecutable)]
        public string Executable { get; }
        [Macro(RadMacros.ProfilerArguments), Description("Profiler Arguments"), DisplayName("Arguments")]
        [DefaultValue(DefaultOptionValues.ProfilerArguments)]
        public string Arguments { get; }
        [Macro(RadMacros.ProfilerWorkingDirectory), Description("Profiler Working Directory"), DisplayName("Working Directory")]
        [DefaultValue(DefaultOptionValues.ProfilerWorkingDirectory)]
        public string WorkingDirectory { get; }
        [Macro(RadMacros.ProfilerOutputPath), Description("Profiler Output Path"), DisplayName("Output Path")]
        [DefaultValue(DefaultOptionValues.ProfilerOutputPath)]
        public string OutputPath { get; }
        [Macro(RadMacros.ProfilerViewerExecutable), Description("Profiler Viewer Executable"), DisplayName("Viewer Executable")]
        [DefaultValue(DefaultOptionValues.ProfilerViewerExecutable)]
        public string ViewerExecutable { get; }
        [Macro(RadMacros.ProfilerViewerArguments), Description("Profiler Viewer Arguments"), DisplayName("Viewer Arguments")]
        [DefaultValue(DefaultOptionValues.ProfilerViewerArguments)]
        public string ViewerArguments { get; }
        [Macro(RadMacros.ProfilerLocalPath), Description("Profiler Local Copy Path"), DisplayName("Local Copy Path")]
        [DefaultValue(DefaultOptionValues.ProfilerLocalOutputCopyPath)]
        public string LocalOutputCopyPath { get; }
        [Description("Profiler Run As Admin"), DisplayName("Run As Admin")]
        [DefaultValue(DefaultOptionValues.ProfilerRunAsAdmin)]
        public bool RunAsAdmin { get; }

        public async Task<ProfilerProfileOptions> EvaluateAsync(IMacroEvaluator macroEvaluator) =>
            new ProfilerProfileOptions(
                executable: await macroEvaluator.GetMacroValueAsync(RadMacros.ProfilerExecutable),
                arguments: await macroEvaluator.GetMacroValueAsync(RadMacros.ProfilerArguments),
                workingDirectory: await macroEvaluator.GetMacroValueAsync(RadMacros.ProfilerWorkingDirectory),
                outputPath: await macroEvaluator.GetMacroValueAsync(RadMacros.ProfilerOutputPath),
                viewerExecutable: await macroEvaluator.GetMacroValueAsync(RadMacros.ProfilerViewerExecutable),
                viewerArguments: await macroEvaluator.GetMacroValueAsync(RadMacros.ProfilerViewerArguments),
                localOutputCopyPath: await macroEvaluator.GetMacroValueAsync(RadMacros.ProfilerLocalPath),
                runAsAdmin: RunAsAdmin);

        [JsonIgnore]
        public OutputFile RemoteOutputFile => new OutputFile(WorkingDirectory, OutputPath, binaryOutput: true);

        public ProfilerProfileOptions(string executable = DefaultOptionValues.ProfilerExecutable, string arguments = DefaultOptionValues.ProfilerExecutable, string workingDirectory = DefaultOptionValues.ProfilerWorkingDirectory, string outputPath = DefaultOptionValues.ProfilerOutputPath, string viewerExecutable = DefaultOptionValues.ProfilerViewerExecutable, string viewerArguments = DefaultOptionValues.ProfilerViewerArguments, string localOutputCopyPath = DefaultOptionValues.ProfilerLocalOutputCopyPath, bool runAsAdmin = DefaultOptionValues.ProfilerRunAsAdmin)
        {
            Executable = executable;
            Arguments = arguments;
            WorkingDirectory = workingDirectory;
            OutputPath = outputPath;
            ViewerExecutable = viewerExecutable;
            ViewerArguments = viewerArguments;
            LocalOutputCopyPath = localOutputCopyPath;
            RunAsAdmin = runAsAdmin;
        }
    }

    public readonly struct ServerConnectionOptions
    {
        public string RemoteMachine { get; }
        public int Port { get; }

        public ServerConnectionOptions(string remoteMachine = "127.0.0.1", int port = 9339)
        {
            RemoteMachine = remoteMachine;
            Port = port;
        }

        public override string ToString() => $"{RemoteMachine}:{Port}";
    }

    public readonly struct OutputFile
    {
        public string Directory { get; }
        public string File { get; }
        public bool BinaryOutput { get; }
        public string[] Path => new[] { Directory, File };

        public OutputFile(string directory = "", string file = "", bool binaryOutput = true)
        {
            Directory = directory;
            File = file;
            BinaryOutput = binaryOutput;
        }
    }
}
