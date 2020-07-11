using Newtonsoft.Json;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading.Tasks;
using VSRAD.Package.ProjectSystem.Macros;
using VSRAD.Package.Utils;

namespace VSRAD.Package.Options
{
    // TODO: remove (obsolete)
    [AttributeUsage(AttributeTargets.Property)]
    public sealed class MacroAttribute : Attribute
    {
        public string MacroName { get; }

        public MacroAttribute(string macroName) => MacroName = macroName;
    }

    // TODO: remove (obsolete)
    [AttributeUsage(AttributeTargets.Property)]
    public sealed class BinaryChoiceAttribute : Attribute
    {
        public (string True, string False) Choice { get; }

        public BinaryChoiceAttribute(string trueString, string falseString)
        {
            Choice = (trueString, falseString);
        }
    }

    public sealed class ProfileOptions : ICloneable
    {
        public GeneralProfileOptions General { get; } = new GeneralProfileOptions();

        public DebuggerProfileOptions Debugger { get; } = new DebuggerProfileOptions();

        [JsonProperty(ItemConverterType = typeof(MacroItemConverter))]
        public ObservableCollection<MacroItem> Macros { get; } = new ObservableCollection<MacroItem>();

        public ObservableCollection<ActionProfileOptions> Actions { get; } = new ObservableCollection<ActionProfileOptions>();

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

    public sealed class ActionProfileOptions
    {
        public string Name { get; set; }

        public const string BuiltinActionDebug = "Debug";

        [JsonProperty(ItemConverterType = typeof(ActionStepJsonConverter))]
        public ObservableCollection<IActionStep> Steps { get; } = new ObservableCollection<IActionStep>();

        public async Task<ActionProfileOptions> EvaluateAsync(IMacroEvaluator evaluator, ProfileOptions profile)
        {
            var evaluated = new ActionProfileOptions { Name = Name };
            foreach (var step in Steps)
                evaluated.Steps.Add(await step.EvaluateAsync(evaluator, profile));
            return evaluated;
        }
    }

    public sealed class GeneralProfileOptions : DefaultNotifyPropertyChanged
    {
        private string _profileName;
        [JsonIgnore]
        public string ProfileName { get => _profileName; set => SetField(ref _profileName, value); }

        private string _remoteMachine = "192.168.0.1";
        public string RemoteMachine { get => _remoteMachine; set => SetField(ref _remoteMachine, value); }

        private int _port = 9339;
        public int Port { get => _port; set => SetField(ref _port, value); }

        private bool _copySources = true;
        public bool CopySources { get => _copySources; set => SetField(ref _copySources, value); }

        private string _deployDirectory = "";
        public string DeployDirectory { get => _deployDirectory; set => SetField(ref _deployDirectory, value); }

        private string _localWorkDir = "$(ProjectDir)";
        public string LocalWorkDir { get => _localWorkDir; set => SetField(ref _localWorkDir, value); }

        private string _remoteWorkDir = "$(" + RadMacros.DeployDirectory + ")";
        public string RemoteWorkDir { get => _remoteWorkDir; set => SetField(ref _remoteWorkDir, value); }

        private string _additionalSources = "";
        public string AdditionalSources { get => _additionalSources; set => SetField(ref _additionalSources, value); }

        [JsonIgnore]
        public ServerConnectionOptions Connection => new ServerConnectionOptions(RemoteMachine, Port);

        public async Task<GeneralProfileOptions> EvaluateAsync(IMacroEvaluator evaluator) => new GeneralProfileOptions
        {
            ProfileName = ProfileName,
            RemoteMachine = RemoteMachine,
            Port = Port,
            CopySources = CopySources,
            DeployDirectory = await evaluator.EvaluateAsync(DeployDirectory),
            LocalWorkDir = await evaluator.EvaluateAsync(LocalWorkDir),
            RemoteWorkDir = await evaluator.EvaluateAsync(RemoteWorkDir),
            AdditionalSources = AdditionalSources
        };

        public async Task<ActionEnvironment> EvaluateActionEnvironmentAsync(IMacroEvaluator evaluator) =>
            new ActionEnvironment(await evaluator.EvaluateAsync(LocalWorkDir), await evaluator.EvaluateAsync(RemoteWorkDir));
    }

    public sealed class DebuggerProfileOptions
    {
        [JsonProperty(ItemConverterType = typeof(ActionStepJsonConverter))]
        public ObservableCollection<IActionStep> Steps { get; } = new ObservableCollection<IActionStep>();

        public BuiltinActionFile OutputFile { get; }
        public BuiltinActionFile WatchesFile { get; }
        public BuiltinActionFile StatusFile { get; }

        public bool BinaryOutput { get; set; }
        public int OutputOffset { get; set; }

        public async Task<DebuggerProfileOptions> EvaluateAsync(IMacroEvaluator evaluator, ProfileOptions profile)
        {
            var evaluated = new DebuggerProfileOptions(
                outputOffset: OutputOffset,
                binaryOutput: BinaryOutput,
                outputFile: await OutputFile.EvaluateAsync(evaluator),
                watchesFile: await WatchesFile.EvaluateAsync(evaluator),
                statusFile: await StatusFile.EvaluateAsync(evaluator));
            foreach (var step in Steps)
                evaluated.Steps.Add(await step.EvaluateAsync(evaluator, profile));
            return evaluated;
        }

        public DebuggerProfileOptions(bool binaryOutput = true, int outputOffset = 0, BuiltinActionFile outputFile = null, BuiltinActionFile watchesFile = null, BuiltinActionFile statusFile = null)
        {
            BinaryOutput = binaryOutput;
            OutputOffset = outputOffset;
            OutputFile = outputFile ?? new BuiltinActionFile();
            WatchesFile = watchesFile ?? new BuiltinActionFile();
            StatusFile = statusFile ?? new BuiltinActionFile();
        }
    }

    public sealed class DisassemblerProfileOptions
    {
        [Macro(RadMacros.DisassemblerExecutable)]
        [Description("Path to the disassembler executable on the remote machine.")]
        [DefaultValue(DefaultOptionValues.DisassemblerExecutable)]
        public string Executable { get; }
        [Macro(RadMacros.DisassemblerArguments), DisplayName("Arguments")]
        [Description("Arguments for Executable.")]
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
        [Description("Arguments for Executable.")]
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
        [Description("Arguments for Viewer Executable.")]
        [DefaultValue(DefaultOptionValues.ProfilerViewerArguments)]
        public string ViewerArguments { get; }
        [Macro(RadMacros.ProfilerLocalPath), DisplayName("Local Copy Path")]
        [Description("Path to the file on local machine to copy profiler output file.")]
        [DefaultValue(DefaultOptionValues.ProfilerLocalOutputCopyPath)]
        public string LocalOutputCopyPath { get; }
        [DisplayName("Run As Administrator")]
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

    public sealed class PreprocessorProfileOptions
    {
        [Macro(RadMacros.PreprocessorExecutable), Description("Path to the preprocessor executable on the remote machine")]
        [DefaultValue(DefaultOptionValues.PreprocessorExecutable)]
        public string Executable { get; }
        [Macro(RadMacros.PreprocessorArguments), Description("Arguments for Executable"), DisplayName("Arguments")]
        [DefaultValue(DefaultOptionValues.BuildArguments)]
        public string Arguments { get; }
        [Macro(RadMacros.PreprocessorWorkingDirectory), Description("Preprocessor Working Directory"), DisplayName("Working Directory")]
        [DefaultValue(DefaultOptionValues.BuildWorkingDirectory)]
        public string WorkingDirectory { get; }
        [Macro(RadMacros.PreprocessorOutputPath), DisplayName("Output Path")]
        [Description("Path to the Preprocessor script output file (can be relative to Working Directory).")]
        [DefaultValue(DefaultOptionValues.PreprocessorOutputPath)]
        public string OutputPath { get; }
        [Macro(RadMacros.PreprocessorLocalPath), DisplayName("Local Path")]
        [Description("Path to the file on local machine to copy Preprocessor output file.")]
        [DefaultValue(DefaultOptionValues.PreprocessorLocalOutputCopyPath)]
        public string LocalOutputCopyPath { get; }
        [Macro(RadMacros.PreprocessorLineMarker), DisplayName("Line Marker")]
        [Description("Preprocessor will search this line in output file and place the cursor on it if this line exists.")]
        [DefaultValue(DefaultOptionValues.PreprocessorLocalOutputCopyPath)]
        public string LineMarker { get; }

        [JsonIgnore]
        public OutputFile RemoteOutputFile => new OutputFile(WorkingDirectory, OutputPath, binaryOutput: true);

        public static async Task<PreprocessorProfileOptions> EvaluateAsync(IMacroEvaluator macroEvaluator) =>
            new PreprocessorProfileOptions(
                executable: await macroEvaluator.GetMacroValueAsync(RadMacros.PreprocessorExecutable),
                arguments: await macroEvaluator.GetMacroValueAsync(RadMacros.PreprocessorArguments),
                workingDirectory: await macroEvaluator.GetMacroValueAsync(RadMacros.PreprocessorWorkingDirectory),
                outputPath: await macroEvaluator.GetMacroValueAsync(RadMacros.PreprocessorOutputPath),
                localOutputCopyPath: await macroEvaluator.GetMacroValueAsync(RadMacros.PreprocessorLocalPath),
                lineMarker: await macroEvaluator.GetMacroValueAsync(RadMacros.PreprocessorLineMarker)
            );

        public PreprocessorProfileOptions(string executable = DefaultOptionValues.PreprocessorExecutable, string arguments = DefaultOptionValues.PreprocessorArguments, string workingDirectory = DefaultOptionValues.PreprocessorWorkingDirectory, string outputPath = DefaultOptionValues.PreprocessorOutputPath, string localOutputCopyPath = DefaultOptionValues.PreprocessorLocalOutputCopyPath, string lineMarker = DefaultOptionValues.PreprocessorLineMarker)
        {
            Executable = executable;
            Arguments = arguments;
            WorkingDirectory = workingDirectory;
            OutputPath = outputPath;
            LocalOutputCopyPath = localOutputCopyPath;
            LineMarker = lineMarker;
        }
    }

    public sealed class BuildProfileOptions
    {
        [Description("Run preprocessor as the first build step."), DisplayName("Run Preprocessor")]
        [DefaultValue(DefaultOptionValues.BuildRunPreprocessor)]
        public bool RunPreprocessor { get; }
        [Description("Run disassembler as the next build step."), DisplayName("Run Disassembler")]
        [DefaultValue(DefaultOptionValues.BuildRunDisassembler)]
        public bool RunDisassembler { get; }
        [Macro(RadMacros.BuildExecutable), DisplayName("Final Build Step Executable")]
        [Description("Path to final build step executable on remote machine. Leave empty to skip third build step.")]
        [DefaultValue(DefaultOptionValues.BuildExecutable)]
        public string Executable { get; }
        [Macro(RadMacros.BuildArguments), DisplayName("Final Build Step Arguments")]
        [Description("Final Build Step Executable arguments.")]
        [DefaultValue(DefaultOptionValues.BuildArguments)]
        public string Arguments { get; }
        [Macro(RadMacros.BuildWorkingDirectory), DisplayName("Final Build Step Working Directory")]
        [Description("Final build step working directory")]
        [DefaultValue(DefaultOptionValues.BuildWorkingDirectory)]
        public string WorkingDirectory { get; }

        public async Task<BuildProfileOptions> EvaluateAsync(IMacroEvaluator macroEvaluator) =>
            new BuildProfileOptions(
                runPreprocessor: RunPreprocessor,
                runDisassembler: RunDisassembler,
                executable: await macroEvaluator.GetMacroValueAsync(RadMacros.BuildExecutable),
                arguments: await macroEvaluator.GetMacroValueAsync(RadMacros.BuildArguments),
                workingDirectory: await macroEvaluator.GetMacroValueAsync(RadMacros.BuildWorkingDirectory)
            );

        public BuildProfileOptions(bool runPreprocessor = DefaultOptionValues.BuildRunPreprocessor, bool runDisassembler = DefaultOptionValues.BuildRunDisassembler, string executable = DefaultOptionValues.BuildExecutable, string arguments = DefaultOptionValues.BuildArguments, string workingDirectory = DefaultOptionValues.BuildWorkingDirectory)
        {
            RunPreprocessor = runPreprocessor;
            RunDisassembler = runDisassembler;
            Executable = executable;
            Arguments = arguments;
            WorkingDirectory = workingDirectory;
        }
    }

    public readonly struct ServerConnectionOptions : IEquatable<ServerConnectionOptions>
    {
        public string RemoteMachine { get; }
        public int Port { get; }

        public ServerConnectionOptions(string remoteMachine = "127.0.0.1", int port = 9339)
        {
            RemoteMachine = remoteMachine;
            Port = port;
        }

        public override string ToString() => $"{RemoteMachine}:{Port}";

        public bool Equals(ServerConnectionOptions s) => RemoteMachine == s.RemoteMachine && Port == s.Port;
        public override bool Equals(object o) => o is ServerConnectionOptions s && Equals(s);
        public override int GetHashCode() => (RemoteMachine, Port).GetHashCode();
        public static bool operator ==(ServerConnectionOptions left, ServerConnectionOptions right) => left.Equals(right);
        public static bool operator !=(ServerConnectionOptions left, ServerConnectionOptions right) => !(left == right);
    }

    public readonly struct ActionEnvironment : IEquatable<ActionEnvironment>
    {
        public string LocalWorkDir { get; }
        public string RemoteWorkDir { get; }

        public ActionEnvironment(string localWorkDir, string remoteWorkDir)
        {
            LocalWorkDir = localWorkDir;
            RemoteWorkDir = remoteWorkDir;
        }

        public bool Equals(ActionEnvironment o) => LocalWorkDir == o.LocalWorkDir && RemoteWorkDir == o.RemoteWorkDir;
        public override bool Equals(object o) => o is ActionEnvironment env && Equals(env);
        public override int GetHashCode() => (LocalWorkDir, RemoteWorkDir).GetHashCode();
        public static bool operator ==(ActionEnvironment left, ActionEnvironment right) => left.Equals(right);
        public static bool operator !=(ActionEnvironment left, ActionEnvironment right) => !(left == right);
    }

    public readonly struct OutputFile : IEquatable<OutputFile>
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

        public bool Equals(OutputFile o) => Directory == o.Directory && File == o.File && BinaryOutput == o.BinaryOutput;
        public override bool Equals(object o) => o is OutputFile f && Equals(f);
        public override int GetHashCode() => (Directory, File, BinaryOutput).GetHashCode();
        public static bool operator ==(OutputFile left, OutputFile right) => left.Equals(right);
        public static bool operator !=(OutputFile left, OutputFile right) => !(left == right);
    }
}
