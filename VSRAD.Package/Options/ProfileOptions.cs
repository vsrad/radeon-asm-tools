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
        public BuildProfileOptions Build { get; }

        public ProfileOptions(GeneralProfileOptions general = null, DebuggerProfileOptions debugger = null, DisassemblerProfileOptions disassembler = null, ProfilerProfileOptions profiler = null, BuildProfileOptions build = null)
        {
            General = general ?? new GeneralProfileOptions();
            Debugger = debugger ?? new DebuggerProfileOptions();
            Disassembler = disassembler ?? new DisassemblerProfileOptions();
            Profiler = profiler ?? new ProfilerProfileOptions();
            Build = build ?? new BuildProfileOptions();
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
        [Macro(RadMacros.DeployDirectory), DisplayName("Deploy Directory")]
        [Description("Directory on the remote machine where the project is deployed before starting the debugger.")]
        [DefaultValue(DefaultOptionValues.DeployDirectory)]
        public string DeployDirectory { get; }
        [DisplayName("Remote Machine Address")]
        [Description("IP address of the remote machine.To debug kernels locally, start the debug server on your local machine and enter `127.0.0.1` in this field.")]
        [DefaultValue(DefaultOptionValues.RemoteMachineAdredd)]
        public string RemoteMachine { get; }
        [Description("Port on the remote machine the debug server is listening on. (When started without arguments, the server listens on port `9339`)")]
        [DefaultValue(DefaultOptionValues.Port)]
        public int Port { get; }
        [DisplayName("Autosave Source")]
        [Description("specifies whether the source files that are changed should be automatically saved before running remote commands (debug, disassemble, profile, etc.). See user manual for more details.")]
        [DefaultValue(DefaultOptionValues.AutosaveSource)]
        public DocumentSaveType AutosaveSource { get; }
        [Description("Enables or disables remote deployment."), DisplayName("Copy Sources to Remote")]
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
        [Macro(RadMacros.DebuggerExecutable)]
        [Description("Path to the debugger executable on the remote machine.")]
        [DefaultValue(DefaultOptionValues.DebuggerExecutable)]
        public string Executable { get; }
        [Macro(RadMacros.DebuggerArguments)]
        [Description("Command-line arguments for Executable.")]
        [DefaultValue(DefaultOptionValues.DebuggerArguments)]
        public string Arguments { get; }
        [Macro(RadMacros.DebuggerWorkingDirectory), DisplayName("Working Directory")]
        [Description("Debugger working directory.")]
        [DefaultValue(DefaultOptionValues.DebuggerWorkingDirectory)]
        public string WorkingDirectory { get; }
        [Macro(RadMacros.DebuggerOutputPath), DisplayName("Output Path")]
        [Description("Path to the debug script output file (can be relative to Working Directory).")]
        [DefaultValue(DefaultOptionValues.DebuggerOutputPath)]
        public string OutputPath { get; }
        [DisplayName("Output Mode"), BooleanDisplayValues("Binary", "Text")]
        [Description("Specifies how the debug script output file is parsed: 'Text': each line is read as a hexadecimal string (0x...), 'Binary': 4-byte blocks are read as a single dword value.")]
        [DefaultValue(DefaultOptionValues.DebuggerBinaryOutput)]
        public bool BinaryOutput { get; }
        [DisplayName("Parse Valid Watches File")]
        [Description("Specifies whether the file specified in Valid Watches File Path should be used to filter valid watches.")]
        [DefaultValue(DefaultOptionValues.DebuggerParseValidWatches)]
        public bool ParseValidWatches { get; }
        [DisplayName("Valid Watches File Path")]
        [Description("Path to the file with valid watch names on the remote machine.")]
        [DefaultValue(DefaultOptionValues.DebuggerValidWatchesFilePath)]
        public string ValidWatchesFilePath { get; }
        [DisplayName("Run As Administrator")]
        [Description("Specifies whether the `Executable` is run with administrator rights.")]
        [DefaultValue(DefaultOptionValues.DebuggerRunAsAdmin)]
        public bool RunAsAdmin { get; }
        [Description("Debugger Timeout (seconds), 0 - timeout disabled"), DisplayName("Timeout")]
        [DefaultValue(DefaultOptionValues.DebuggerTimeoutSecs)]
        public int TimeoutSecs { get; }
        [Description("Output file offset: bytes if output mode is binary, lines if output mode is text"), DisplayName("Output Offset")]
        [DefaultValue(DefaultOptionValues.OutputOffset)]
        public int OutputOffset { get; }

        [JsonIgnore]
        public OutputFile RemoteOutputFile => new OutputFile(WorkingDirectory, OutputPath, BinaryOutput);
        [JsonIgnore]
        public OutputFile ValidWatchesFile => new OutputFile(WorkingDirectory, ValidWatchesFilePath);

        public async Task<DebuggerProfileOptions> EvaluateAsync(IMacroEvaluator macroEvaluator) =>
            new DebuggerProfileOptions(
                executable: await macroEvaluator.GetMacroValueAsync(RadMacros.DebuggerExecutable),
                arguments: await macroEvaluator.GetMacroValueAsync(RadMacros.DebuggerArguments),
                workingDirectory: await macroEvaluator.GetMacroValueAsync(RadMacros.DebuggerWorkingDirectory),
                outputPath: await macroEvaluator.GetMacroValueAsync(RadMacros.DebuggerOutputPath),
                outputOffset: OutputOffset,
                binaryOutput: BinaryOutput,
                runAsAdmin: RunAsAdmin,
                timeoutSecs: TimeoutSecs,
                parseValidWatches: ParseValidWatches,
                validWatchesFilePath: ValidWatchesFilePath);

        public DebuggerProfileOptions(string executable = null, string arguments = null, string workingDirectory = DefaultOptionValues.DebuggerWorkingDirectory, string outputPath = DefaultOptionValues.DebuggerOutputPath, bool binaryOutput = DefaultOptionValues.DebuggerBinaryOutput, bool runAsAdmin = DefaultOptionValues.DebuggerRunAsAdmin, int timeoutSecs = DefaultOptionValues.DebuggerTimeoutSecs, bool parseValidWatches = DefaultOptionValues.DebuggerParseValidWatches, string validWatchesFilePath = DefaultOptionValues.DebuggerValidWatchesFilePath, int outputOffset = 0)
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
            OutputOffset = outputOffset;
        }
    }

    public sealed class DisassemblerProfileOptions
    {
        [Macro(RadMacros.DisassemblerExecutable)]
        [Description("Path to the disassembler executable on the remote machine.")]
        [DefaultValue(DefaultOptionValues.DisassemblerExecutable)]
        public string Executable { get; }
        [Macro(RadMacros.DisassemblerArguments), DisplayName("Arguments")]
        [Description("Command-line arguments for Executable.")]
        [DefaultValue(DefaultOptionValues.DisassemblerArguments)]
        public string Arguments { get; }
        [Macro(RadMacros.DisassemblerWorkingDirectory), DisplayName("Working Directory")]
        [Description("Disassembler Working Directory")]
        [DefaultValue(DefaultOptionValues.DisassemblerWorkingDirectory)]
        public string WorkingDirectory { get; }
        [Macro(RadMacros.DisassemblerOutputPath), DisplayName("Output Path")]
        [Description("Path to the disassembler script output file (can be relative to Working Directory).")]
        [DefaultValue(DefaultOptionValues.DisassemblerOutputPath)]
        public string OutputPath { get; }
        [Macro(RadMacros.DisassemblerLocalPath), DisplayName("Local Path")]
        [Description("Path to the file on local machine to copy disassembler output file.")]
        [DefaultValue(DefaultOptionValues.DisassemblerLocalOutputCopyPath)]
        public string LocalOutputCopyPath { get; }
        [DisplayName("Line Marker")]
        [Description("Disassembler will search this line in output file and place the cursor on it if this line exists.")]
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
        [Macro(RadMacros.ProfilerExecutable)]
        [Description("Path to the profiler executable on the remote machine.")]
        [DefaultValue(DefaultOptionValues.ProfilerExecutable)]
        public string Executable { get; }
        [Macro(RadMacros.ProfilerArguments), DisplayName("Arguments")]
        [Description("Command-line arguments for Executable.")]
        [DefaultValue(DefaultOptionValues.ProfilerArguments)]
        public string Arguments { get; }
        [Macro(RadMacros.ProfilerWorkingDirectory), DisplayName("Working Directory")]
        [Description("Profiler Working Directory")]
        [DefaultValue(DefaultOptionValues.ProfilerWorkingDirectory)]
        public string WorkingDirectory { get; }
        [Macro(RadMacros.ProfilerOutputPath), DisplayName("Output Path")]
        [Description("Path to the profiler script output file (can be relative to Working Directory).")]
        [DefaultValue(DefaultOptionValues.ProfilerOutputPath)]
        public string OutputPath { get; }
        [Macro(RadMacros.ProfilerViewerExecutable), DisplayName("Viewer Executable")]
        [Description("Path to the viewer executable on the local machine.")]
        [DefaultValue(DefaultOptionValues.ProfilerViewerExecutable)]
        public string ViewerExecutable { get; }
        [Macro(RadMacros.ProfilerViewerArguments), DisplayName("Viewer Arguments")]
        [Description("Command-line arguments for Viewer Executable.")]
        [DefaultValue(DefaultOptionValues.ProfilerViewerArguments)]
        public string ViewerArguments { get; }
        [Macro(RadMacros.ProfilerLocalPath), DisplayName("Local Copy Path")]
        [Description("Path to the file on local machine to copy profiler output file.")]
        [DefaultValue(DefaultOptionValues.ProfilerLocalOutputCopyPath)]
        public string LocalOutputCopyPath { get; }
        [DisplayName("Run As Admin")]
        [Description("Specifies whether the `Executable` is run with administrator rights.")]
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

    public sealed class BuildProfileOptions
    {
        [Macro(RadMacros.BuildExecutable), Description("Executable")]
        [DefaultValue(DefaultOptionValues.BuildExecutable)]
        public string Executable { get; }
        [Macro(RadMacros.BuildArguments), Description("Build Arguments"), DisplayName("Arguments")]
        [DefaultValue(DefaultOptionValues.BuildArguments)]
        public string Arguments { get; }
        [Macro(RadMacros.BuildWorkingDirectory), Description("Build Working Directory"), DisplayName("Working Directory")]
        [DefaultValue(DefaultOptionValues.BuildWorkingDirectory)]
        public string WorkingDirectory { get; }

        [Macro(RadMacros.BuildPreprocessedSource), DisplayName("Preprocessed Source")]
        [Description(@"Path to the preprocessed source on the remote machine. Used to map line numbers in the compiler's output to original sources. Linemarkers should start with '//# ' or '# '")]
        [DefaultValue(DefaultOptionValues.BuildPreprocessedSource)]
        public string PreprocessedSource { get; }

        [JsonIgnore]
        public OutputFile PreprocessedSourceFile => new OutputFile(WorkingDirectory, PreprocessedSource, binaryOutput: true);

        public async Task<BuildProfileOptions> EvaluateAsync(IMacroEvaluator macroEvaluator) =>
            new BuildProfileOptions(
                executable: await macroEvaluator.GetMacroValueAsync(RadMacros.BuildExecutable),
                arguments: await macroEvaluator.GetMacroValueAsync(RadMacros.BuildArguments),
                workingDirectory: await macroEvaluator.GetMacroValueAsync(RadMacros.BuildWorkingDirectory),
                preprocessedSource: await macroEvaluator.GetMacroValueAsync(RadMacros.BuildPreprocessedSource)
            );

        public BuildProfileOptions(string executable = DefaultOptionValues.BuildExecutable, string arguments = DefaultOptionValues.BuildArguments, string workingDirectory = DefaultOptionValues.BuildWorkingDirectory, string preprocessedSource = DefaultOptionValues.BuildPreprocessedSource)
        {
            Executable = executable;
            Arguments = arguments;
            WorkingDirectory = workingDirectory;
            PreprocessedSource = preprocessedSource;
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
