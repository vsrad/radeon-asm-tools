using Newtonsoft.Json;
using System;
using System.Collections.ObjectModel;
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

        public MenuCommandProfileOptions MenuCommands { get; } = new MenuCommandProfileOptions();

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

    public sealed class MenuCommandProfileOptions : DefaultNotifyPropertyChanged
    {
        private string _profileAction;
        public string ProfileAction { get => _profileAction; set => SetField(ref _profileAction, value ?? ""); }

        private string _disassembleAction;
        public string DisassembleAction { get => _disassembleAction; set => SetField(ref _disassembleAction, value ?? ""); }

        private string _preprocessAction;
        public string PreprocessAction { get => _preprocessAction; set => SetField(ref _preprocessAction, value ?? ""); }
    }

    public sealed class ActionNameChangedEventArgs : EventArgs
    {
        public string OldName { get; set; }
        public string NewName { get; set; }
    }

    public sealed class ActionProfileOptions : DefaultNotifyPropertyChanged
    {
        public event EventHandler<ActionNameChangedEventArgs> NameChanged;

        private string _name;
        public string Name
        {
            get => _name;
            set
            {
                NameChanged?.Invoke(this, new ActionNameChangedEventArgs { OldName = _name, NewName = value });
                SetField(ref _name, value);
            }
        }

        public const string BuiltinActionDebug = "Debug";

        [JsonProperty(ItemConverterType = typeof(ActionStepJsonConverter))]
        public ObservableCollection<IActionStep> Steps { get; } = new ObservableCollection<IActionStep>();

        public async Task<Result<ActionProfileOptions>> EvaluateAsync(IMacroEvaluator evaluator, ProfileOptions profile)
        {
            var evaluated = new ActionProfileOptions { Name = Name };
            foreach (var step in Steps)
            {
                if ((await step.EvaluateAsync(evaluator, profile, Name)).TryGetResult(out var evaluatedStep, out var error))
                    evaluated.Steps.Add(evaluatedStep);
                else
                    return error;
            }
            return evaluated;
        }
    }

    public sealed class GeneralProfileOptions : DefaultNotifyPropertyChanged
    {
        private string _profileName;
        [JsonIgnore]
        public string ProfileName { get => _profileName; set => SetField(ref _profileName, value); }

        private string _remoteMachine = "127.0.0.1";
        public string RemoteMachine { get => _remoteMachine; set => SetField(ref _remoteMachine, value); }

        private int _port = 9339;
        public int Port { get => _port; set => SetField(ref _port, value); }

        private bool _copySources = true;
        public bool CopySources { get => _copySources; set => SetField(ref _copySources, value); }

        private bool _continueActionExecOnError = false;
        public bool ContinueActionExecOnError { get => _continueActionExecOnError; set => SetField(ref _continueActionExecOnError, value); }

        private string _deployDirectory = "$(" + CleanProfileMacros.RemoteWorkDir + ")";
        public string DeployDirectory { get => _deployDirectory; set => SetField(ref _deployDirectory, value); }

        private string _localWorkDir = "$(" + CleanProfileMacros.LocalWorkDir + ")";
        public string LocalWorkDir { get => _localWorkDir; set => SetField(ref _localWorkDir, value); }

        private string _remoteWorkDir = "$(" + CleanProfileMacros.RemoteWorkDir + ")";
        public string RemoteWorkDir { get => _remoteWorkDir; set => SetField(ref _remoteWorkDir, value); }

        private string _additionalSources = "";
        public string AdditionalSources { get => _additionalSources; set => SetField(ref _additionalSources, value); }

        [JsonIgnore]
        public ServerConnectionOptions Connection => new ServerConnectionOptions(RemoteMachine, Port);

        public async Task<Result<GeneralProfileOptions>> EvaluateAsync(IMacroEvaluator evaluator)
        {
            var deployDirResult = await evaluator.EvaluateAsync(DeployDirectory);
            if (!deployDirResult.TryGetResult(out var evaluatedDeployDir, out var error))
                return error;
            var envResult = await EvaluateActionEnvironmentAsync(evaluator);
            if (!envResult.TryGetResult(out var evaluatedEnv, out error))
                return error;

            return new GeneralProfileOptions
            {
                ProfileName = ProfileName,
                RemoteMachine = RemoteMachine,
                Port = Port,
                CopySources = CopySources,
                DeployDirectory = evaluatedDeployDir,
                LocalWorkDir = evaluatedEnv.LocalWorkDir,
                RemoteWorkDir = evaluatedEnv.RemoteWorkDir,
                AdditionalSources = AdditionalSources
            };
        }

        public async Task<Result<ActionEnvironment>> EvaluateActionEnvironmentAsync(IMacroEvaluator evaluator)
        {
            var localDirResult = await evaluator.EvaluateAsync(LocalWorkDir);
            if (!localDirResult.TryGetResult(out var evaluatedLocalDir, out var error))
                return error;
            var remoteDirResult = await evaluator.EvaluateAsync(RemoteWorkDir);
            if (!remoteDirResult.TryGetResult(out var evaluatedRemoteDir, out error))
                return error;

            return new ActionEnvironment(evaluatedLocalDir, evaluatedRemoteDir);
        }
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

        public async Task<Result<DebuggerProfileOptions>> EvaluateAsync(IMacroEvaluator evaluator, ProfileOptions profile)
        {
            var outputResult = await OutputFile.EvaluateAsync(evaluator);
            if (!outputResult.TryGetResult(out var outputFile, out var error))
                return error;
            var watchesResult = await WatchesFile.EvaluateAsync(evaluator);
            if (!watchesResult.TryGetResult(out var watchesFile, out error))
                return error;
            var statusResult = await StatusFile.EvaluateAsync(evaluator);
            if (!statusResult.TryGetResult(out var statusFile, out error))
                return error;

            var evaluated = new DebuggerProfileOptions(outputOffset: OutputOffset, binaryOutput: BinaryOutput,
                outputFile: outputFile, watchesFile: watchesFile, statusFile: statusFile);

            foreach (var step in Steps)
            {
                var evalResult = await step.EvaluateAsync(evaluator, profile, ActionProfileOptions.BuiltinActionDebug);
                if (evalResult.TryGetResult(out var evaluatedStep, out error))
                    evaluated.Steps.Add(evaluatedStep);
                else
                    return error;
            }

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
