using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using VSRAD.DebugServer.IPC;
using VSRAD.Package.ProjectSystem.Macros;
using VSRAD.Package.Utils;
using static VSRAD.Package.Options.ActionExtensions;

namespace VSRAD.Package.Options
{
    // Note: when adding a new step, don't forget to add the new type to ActionStepJsonConverter.

    // Note: steps override GetHashCode and set it to a constant value.
    // While this leads to poor performance when used as a key in hash-based collections,
    // it satisfies the contract (the same hash code is returned for objects that are equal)
    // and prevents errors when an object is mutated (e.g. in profile editor's WPF controls, https://stackoverflow.com/q/15365905)

    public sealed class ActionEvaluationEnvironment
    {
        public string LocalWorkDir { get; }
        public string RemoteWorkDir { get; }
        public bool RunActionsLocally { get; }
        public CapabilityInfo ServerCapabilities { get; }
        public IEnumerable<ActionProfileOptions> Actions { get; }

        public ActionEvaluationEnvironment(string localWorkDir, string remoteWorkDir, bool runActionsLocally, CapabilityInfo serverCapabilities, IEnumerable<ActionProfileOptions> actions)
        {
            LocalWorkDir = localWorkDir;
            RemoteWorkDir = remoteWorkDir;
            RunActionsLocally = runActionsLocally;
            ServerCapabilities = serverCapabilities;
            Actions = actions;
        }

        public bool TryResolveFullPath(string path, StepEnvironment location, out string fullPath, out string error)
        {
            try
            {
                if (location == StepEnvironment.Remote)
                {
                    if (ServerCapabilities.Platform == ServerPlatform.Windows)
                    {
                        fullPath = Path.Combine(RemoteWorkDir, path);
                    }
                    else
                    {
                        if (RemoteWorkDir.Length == 0 || path.StartsWith('/'))
                            fullPath = path;
                        else if (RemoteWorkDir.EndsWith('/'))
                            fullPath = RemoteWorkDir + path;
                        else
                            fullPath = RemoteWorkDir + '/' + path;
                    }
                }
                else
                {
                    fullPath = Path.Combine(LocalWorkDir, path);
                }
                error = "";
                return true;
            }
            catch (ArgumentException e) when (e.Message == "Illegal characters in path.")
            {
                var workDir = location == StepEnvironment.Remote ? RemoteWorkDir : LocalWorkDir;
                error = $"Path contains illegal characters: \"{path}\"\r\nWorking directory: \"{workDir}\"";
                fullPath = null;
                return false;
            }
        }
    }

    public interface IActionStep : INotifyPropertyChanged
    {
        Task<Result<IActionStep>> EvaluateAsync(IMacroEvaluator evaluator, ActionEvaluationEnvironment env, string sourceAction);
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum StepEnvironment
    {
        Remote, Local
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum FileCopyDirection
    {
        RemoteToLocal, LocalToRemote, LocalToLocal
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum ActionIfNotModified
    {
        DoNotCopy, Copy, Fail
    }

    public static class ActionExtensions
    {
        public static bool IsRemote(this BuiltinActionFile file) =>
            file.Location == StepEnvironment.Remote;

        public static Error EvaluationError(string action, string step, string description) =>
            new Error(description, title: $"Action \"{action}\" could not be run due to a misconfigured {step} step");
    }

    public sealed class CopyFileStep : DefaultNotifyPropertyChanged, IActionStep
    {
        private FileCopyDirection _direction;
        public FileCopyDirection Direction { get => _direction; set => SetField(ref _direction, value); }

        private string _sourcePath = "";
        public string SourcePath { get => _sourcePath; set => SetField(ref _sourcePath, value); }

        private string _targetPath = "";
        public string TargetPath { get => _targetPath; set => SetField(ref _targetPath, value); }

        private ActionIfNotModified _ifNotModified;
        public ActionIfNotModified IfNotModified { get => _ifNotModified; set => SetField(ref _ifNotModified, value); }

        private bool _preserveTimestamps;
        public bool PreserveTimestamps { get => _preserveTimestamps; set => SetField(ref _preserveTimestamps, value); }

        private bool _includeSubdirectories;
        public bool IncludeSubdirectories { get => _includeSubdirectories; set => SetField(ref _includeSubdirectories, value); }

        private bool _useCompression;
        public bool UseCompression { get => _useCompression; set => SetField(ref _useCompression, value); }

        public override string ToString()
        {
            if (string.IsNullOrWhiteSpace(SourcePath) || string.IsNullOrWhiteSpace(TargetPath))
                return "Copy File/Directory";

            var dir = Direction == FileCopyDirection.LocalToRemote ? "to Remote"
                    : Direction == FileCopyDirection.RemoteToLocal ? "from Remote"
                    : "Local";

            return $"Copy {dir} {SourcePath} -> {TargetPath}";
        }

        public override bool Equals(object obj) =>
            obj is CopyFileStep step &&
            SourcePath == step.SourcePath &&
            TargetPath == step.TargetPath &&
            IfNotModified == step.IfNotModified &&
            PreserveTimestamps == step.PreserveTimestamps &&
            IncludeSubdirectories == step.IncludeSubdirectories &&
            UseCompression == step.UseCompression;

        public override int GetHashCode() => 1;

        public async Task<Result<IActionStep>> EvaluateAsync(IMacroEvaluator evaluator, ActionEvaluationEnvironment env, string sourceAction)
        {
            if (string.IsNullOrWhiteSpace(SourcePath))
                return EvaluationError(sourceAction, "Copy File", "No source path specified");
            if (string.IsNullOrWhiteSpace(TargetPath))
                return EvaluationError(sourceAction, "Copy File", "No target path specified");

            var sourcePathResult = await evaluator.EvaluateAsync(SourcePath);
            if (!sourcePathResult.TryGetResult(out var evaluatedSourcePath, out var error))
                return error;
            var targetPathResult = await evaluator.EvaluateAsync(TargetPath);
            if (!targetPathResult.TryGetResult(out var evaluatedTargetPath, out error))
                return error;

            if (string.IsNullOrWhiteSpace(evaluatedSourcePath))
                return EvaluationError(sourceAction, "Copy File", $"The specified source path (\"{SourcePath}\") evaluates to an empty string");
            if (string.IsNullOrWhiteSpace(evaluatedTargetPath))
                return EvaluationError(sourceAction, "Copy File", $"The specified target path (\"{TargetPath}\") evaluates to an empty string");

            var direction = env.RunActionsLocally ? FileCopyDirection.LocalToLocal : Direction;

            var sourceLocation = direction == FileCopyDirection.RemoteToLocal ? StepEnvironment.Remote : StepEnvironment.Local;
            var targetLocation = direction == FileCopyDirection.LocalToRemote ? StepEnvironment.Remote : StepEnvironment.Local;
            if (!env.TryResolveFullPath(evaluatedSourcePath, sourceLocation, out evaluatedSourcePath, out var errorMessage))
                return EvaluationError(sourceAction, "Copy File", errorMessage);
            if (!env.TryResolveFullPath(evaluatedTargetPath, targetLocation, out evaluatedTargetPath, out errorMessage))
                return EvaluationError(sourceAction, "Copy File", errorMessage);

            return new CopyFileStep
            {
                Direction = direction,
                SourcePath = evaluatedSourcePath,
                TargetPath = evaluatedTargetPath,
                IfNotModified = IfNotModified,
                PreserveTimestamps = PreserveTimestamps,
                IncludeSubdirectories = IncludeSubdirectories,
                UseCompression = UseCompression
            };
        }
    }

    public sealed class ExecuteStep : DefaultNotifyPropertyChanged, IActionStep
    {
        private StepEnvironment _environment;
        public StepEnvironment Environment { get => _environment; set => SetField(ref _environment, value); }

        private string _executable = "";
        public string Executable { get => _executable; set => SetField(ref _executable, value); }

        private string _arguments = "";
        public string Arguments { get => _arguments; set => SetField(ref _arguments, value); }

        private string _workingDirectory = "";
        public string WorkingDirectory { get => _workingDirectory; set => SetField(ref _workingDirectory, value); }

        private bool _runAsAdmin;
        public bool RunAsAdmin { get => _runAsAdmin; set => SetField(ref _runAsAdmin, value); }

        private bool _waitForCompletion = true;
        public bool WaitForCompletion { get => _waitForCompletion; set => SetField(ref _waitForCompletion, value); }

        private int _timeoutSecs = 0;
        public int TimeoutSecs { get => _timeoutSecs; set => SetField(ref _timeoutSecs, value); }

        private bool _confirmTerminationOnTimeout = true;
        public bool ConfirmTerminationOnTimeout { get => _confirmTerminationOnTimeout; set => SetField(ref _confirmTerminationOnTimeout, value); }

        private string _environmentVariables = "";
        public string EnvironmentVariables { get => _environmentVariables; set => SetField(ref _environmentVariables, value); }

        public override string ToString()
        {
            if (string.IsNullOrWhiteSpace(Executable))
                return "Execute";

            var env = Environment == StepEnvironment.Remote ? "Remote" : "Local";
            return $"Execute {env} {Executable} {Arguments}";
        }

        public override bool Equals(object obj) =>
            obj is ExecuteStep step &&
            Environment == step.Environment &&
            Executable == step.Executable &&
            Arguments == step.Arguments &&
            WorkingDirectory == step.WorkingDirectory &&
            RunAsAdmin == step.RunAsAdmin &&
            WaitForCompletion == step.WaitForCompletion &&
            TimeoutSecs == step.TimeoutSecs &&
            ConfirmTerminationOnTimeout == step.ConfirmTerminationOnTimeout;

        public override int GetHashCode() => 3;

        public async Task<Result<IActionStep>> EvaluateAsync(IMacroEvaluator evaluator, ActionEvaluationEnvironment env, string sourceAction)
        {
            if (string.IsNullOrWhiteSpace(Executable))
                return EvaluationError(sourceAction, "Execute", "No executable specified");

            if (env.ServerCapabilities.Capabilities != null && // server capabilities is null while we editing action without running debug session
                !string.IsNullOrWhiteSpace(EnvironmentVariables) &&
                !env.ServerCapabilities.Capabilities.Contains(ServerCapability.EnvVarSet))
                return EvaluationError(sourceAction, "Execute", "Remote environment variables are set, but Debug Server does not support this functionality. Please, get the latest Debug Server.");

            var executableResult = await evaluator.EvaluateAsync(Executable);
            if (!executableResult.TryGetResult(out var evaluatedExecutable, out var error))
                return error;
            var argumentsResult = await evaluator.EvaluateAsync(Arguments);
            if (!argumentsResult.TryGetResult(out var evaluatedArguments, out error))
                return error;
            var workdirResult = await evaluator.EvaluateAsync(WorkingDirectory);
            if (!workdirResult.TryGetResult(out var evaluatedWorkdir, out error))
                return error;

            if (string.IsNullOrWhiteSpace(evaluatedExecutable))
                return EvaluationError(sourceAction, "Execute", $"The specified executable (\"{Executable}\") evaluates to an empty string");

            var environment = env.RunActionsLocally ? StepEnvironment.Local : Environment;

            if (string.IsNullOrWhiteSpace(evaluatedWorkdir))
            {
                if (environment == StepEnvironment.Remote)
                    evaluatedWorkdir = env.RemoteWorkDir;
                else
                    evaluatedWorkdir = env.LocalWorkDir;
            }

            return new ExecuteStep
            {
                Environment = environment,
                Executable = evaluatedExecutable,
                Arguments = evaluatedArguments,
                WorkingDirectory = evaluatedWorkdir,
                RunAsAdmin = RunAsAdmin,
                // The option to wait for completion is hidden in the UI for remote execution (because IPC.Commands.Execute does not support it).
                // Hence it is always true for a remote ExecuteStep. If it's forced to run locally, we should respect this behavior.
                WaitForCompletion = Environment == StepEnvironment.Remote || WaitForCompletion,
                TimeoutSecs = TimeoutSecs,
                ConfirmTerminationOnTimeout = ConfirmTerminationOnTimeout,
                EnvironmentVariables = EnvironmentVariables
            };
        }
    }

    public sealed class OpenInEditorStep : DefaultNotifyPropertyChanged, IActionStep
    {
        private string _path = "";
        public string Path { get => _path; set => SetField(ref _path, value); }

        private string _lineMarker = "";
        public string LineMarker { get => _lineMarker; set => SetField(ref _lineMarker, value); }

        public override string ToString() =>
            string.IsNullOrWhiteSpace(Path) ? "Open in Editor" : $"Open {Path}";

        public override bool Equals(object obj) =>
            obj is OpenInEditorStep step && Path == step.Path && LineMarker == step.LineMarker;

        public override int GetHashCode() => 5;

        public async Task<Result<IActionStep>> EvaluateAsync(IMacroEvaluator evaluator, ActionEvaluationEnvironment env, string sourceAction)
        {
            if (string.IsNullOrWhiteSpace(Path))
                return EvaluationError(sourceAction, "Open in Editor", "No file path specified");

            var pathResult = await evaluator.EvaluateAsync(Path);
            if (!pathResult.TryGetResult(out var evaluatedPath, out var error))
                return error;

            if (string.IsNullOrWhiteSpace(evaluatedPath))
                return EvaluationError(sourceAction, "Open in Editor", $"The specified file path (\"{Path}\") evaluates to an empty string");

            return new OpenInEditorStep { Path = evaluatedPath, LineMarker = LineMarker };
        }
    }

    public sealed class RunActionStep : DefaultNotifyPropertyChanged, IActionStep
    {
        private string _name = "";
        public string Name { get => _name; set => SetField(ref _name, value); }

        [JsonIgnore]
        public List<IActionStep> EvaluatedSteps { get; }

        public RunActionStep() : this(null) { }

        public RunActionStep(List<IActionStep> evaluatedSteps)
        {
            EvaluatedSteps = evaluatedSteps;
        }

        public override string ToString() =>
            string.IsNullOrWhiteSpace(Name) ? "Run Action" : $"Run {Name}";

        public override bool Equals(object obj) => obj is RunActionStep step && Name == step.Name;

        public override int GetHashCode() => 7;

        public Task<Result<IActionStep>> EvaluateAsync(IMacroEvaluator evaluator, ActionEvaluationEnvironment env, string sourceAction)
        {
            var traversed = new List<string>() { sourceAction };
            return EvaluateAsync(evaluator, env, traversed);
        }

        public async Task<Result<IActionStep>> EvaluateAsync(IMacroEvaluator evaluator, ActionEvaluationEnvironment env, List<string> actionsTraversed)
        {
            if (string.IsNullOrWhiteSpace(Name))
            {
                var message = "No action specified";
                if (actionsTraversed.Count > 1)
                    message += ", required by " + string.Join(" -> ", actionsTraversed.Select(a => "\"" + a + "\""));
                return EvaluationError(actionsTraversed[0], "Run Action", message);
            }
            if (actionsTraversed.Contains(Name))
            {
                var t = string.Join(" -> ", actionsTraversed.Select(a => "\"" + a + "\"")) + " -> \"" + Name + "\"";
                return EvaluationError(actionsTraversed[0], "Run Action", "Encountered a circular dependency: " + t);
            }

            var action = env.Actions.FirstOrDefault(a => a.Name == Name);
            if (action == null)
            {
                var message = "Action \"" + Name + "\" is not found";
                if (actionsTraversed.Count > 1)
                    message += ", required by " + string.Join(" -> ", actionsTraversed.Select(a => "\"" + a + "\""));
                return EvaluationError(actionsTraversed[0], "Run Action", message);
            }

            actionsTraversed.Add(Name);

            var evaluatedSteps = new List<IActionStep>();
            foreach (var step in action.Steps)
            {
                IActionStep evaluated;
                if (step is RunActionStep runAction)
                {
                    var evalResult = await runAction.EvaluateAsync(evaluator, env, actionsTraversed);
                    if (!evalResult.TryGetResult(out evaluated, out var error))
                        return error;
                }
                else
                {
                    var evalResult = await step.EvaluateAsync(evaluator, env, actionsTraversed[0]);
                    if (!evalResult.TryGetResult(out evaluated, out _))
                    {
                        var message = "Action \"" + Name + "\" is misconfigured";
                        actionsTraversed.Remove(Name);
                        if (actionsTraversed.Count > 1)
                            message += ", required by " + string.Join(" -> ", actionsTraversed.Select(a => "\"" + a + "\""));
                        return EvaluationError(actionsTraversed[0], "Run Action", message);
                    }
                }
                evaluatedSteps.Add(evaluated);
            }

            actionsTraversed.RemoveAt(actionsTraversed.Count - 1);

            return new RunActionStep(evaluatedSteps) { Name = Name };
        }
    }

    public sealed class ReadDebugDataStep : DefaultNotifyPropertyChanged, IActionStep
    {
        private BuiltinActionFile _outputFile;
        public BuiltinActionFile OutputFile { get => _outputFile; set => SetField(ref _outputFile, value, ignoreNull: true); }

        private BuiltinActionFile _watchesFile;
        public BuiltinActionFile WatchesFile { get => _watchesFile; set => SetField(ref _watchesFile, value, ignoreNull: true); }

        private BuiltinActionFile _dispatchParamsFile;
        public BuiltinActionFile DispatchParamsFile { get => _dispatchParamsFile; set => SetField(ref _dispatchParamsFile, value, ignoreNull: true); }

        private bool _binaryOutput = true;
        public bool BinaryOutput { get => _binaryOutput; set => SetField(ref _binaryOutput, value); }

        private int _outputOffset = 0;
        public int OutputOffset { get => _outputOffset; set => SetField(ref _outputOffset, value); }

        public ReadDebugDataStep() : this(new BuiltinActionFile(), new BuiltinActionFile(), new BuiltinActionFile(), binaryOutput: true, outputOffset: 0) { }

        public ReadDebugDataStep(BuiltinActionFile outputFile, BuiltinActionFile watchesFile, BuiltinActionFile dispatchParamsFile, bool binaryOutput, int outputOffset)
        {
            OutputFile = outputFile;
            WatchesFile = watchesFile;
            DispatchParamsFile = dispatchParamsFile;
            BinaryOutput = binaryOutput;
            OutputOffset = outputOffset;
        }

        public async Task<Result<IActionStep>> EvaluateAsync(IMacroEvaluator evaluator, ActionEvaluationEnvironment env, string sourceAction)
        {
            var outputResult = await OutputFile.EvaluateAsync(evaluator);
            if (!outputResult.TryGetResult(out var outputFile, out var error))
                return EvaluationError(sourceAction, "Read Debug Data", error.Message);

            var watchesResult = await WatchesFile.EvaluateAsync(evaluator);
            if (!watchesResult.TryGetResult(out var watchesFile, out error))
                return EvaluationError(sourceAction, "Read Debug Data", error.Message);

            var dispatchParamsResult = await DispatchParamsFile.EvaluateAsync(evaluator);
            if (!dispatchParamsResult.TryGetResult(out var dispatchParamsFile, out error))
                return EvaluationError(sourceAction, "Read Debug Data", error.Message);

            if (env.RunActionsLocally)
            {
                outputFile.Location = StepEnvironment.Local;
                watchesFile.Location = StepEnvironment.Local;
                dispatchParamsFile.Location = StepEnvironment.Local;
            }

            if (string.IsNullOrEmpty(outputFile.Path))
            {
                return EvaluationError(sourceAction, "Read Debug Data", "Debug data path is not specified");
            }
            else
            {
                if (!env.TryResolveFullPath(outputFile.Path, outputFile.Location, out var outputFullPath, out var errorMessage))
                    return EvaluationError(sourceAction, "Read Debug Data", errorMessage);
                outputFile.Path = outputFullPath;
            }
            if (!string.IsNullOrEmpty(watchesFile.Path))
            {
                if (!env.TryResolveFullPath(watchesFile.Path, watchesFile.Location, out var watchesFullPath, out var errorMessage))
                    return EvaluationError(sourceAction, "Read Debug Data", errorMessage);
                watchesFile.Path = watchesFullPath;
            }
            if (!string.IsNullOrEmpty(dispatchParamsFile.Path))
            {
                if (!env.TryResolveFullPath(dispatchParamsFile.Path, dispatchParamsFile.Location, out var dispatchParamsFullPath, out var errorMessage))
                    return EvaluationError(sourceAction, "Read Debug Data", errorMessage);
                dispatchParamsFile.Path = dispatchParamsFullPath;
            }

            return new ReadDebugDataStep(outputOffset: OutputOffset, binaryOutput: BinaryOutput,
                outputFile: outputFile, watchesFile: watchesFile, dispatchParamsFile: dispatchParamsFile);
        }

        public override string ToString()
        {
            if (string.IsNullOrWhiteSpace(OutputFile.Path))
                return "Read Debug Data";

            var env = OutputFile.Location == StepEnvironment.Remote ? "Remote" : "Local";
            return $"Read Debug Data {env} {OutputFile.Path}";
        }

        public override int GetHashCode() => 9;

        public override bool Equals(object obj) =>
            obj is ReadDebugDataStep step &&
            OutputFile.Equals(step.OutputFile) &&
            WatchesFile.Equals(step.WatchesFile) &&
            DispatchParamsFile.Equals(step.DispatchParamsFile) &&
            BinaryOutput == step.BinaryOutput &&
            OutputOffset == step.OutputOffset;
    }

    public sealed class ActionStepJsonConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType) =>
            typeof(IActionStep).IsAssignableFrom(objectType);

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            var obj = JObject.Load(reader);
            var step = InstantiateStepFromTypeField((string)obj["Type"]);
            serializer.Populate(obj.CreateReader(), step);
            return step;
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            var serialized = JObject.FromObject(value);
            serialized["Type"] = GetStepType(value);
            serialized.WriteTo(writer);
        }

        private static IActionStep InstantiateStepFromTypeField(string type)
        {
            switch (type)
            {
                case "Execute": return new ExecuteStep();
                case "CopyFile": return new CopyFileStep();
                case "OpenInEditor": return new OpenInEditorStep();
                case "RunAction": return new RunActionStep();
                case "ReadDebugData": return new ReadDebugDataStep();
            }
            throw new ArgumentException($"Unknown step type identifer {type}", nameof(type));
        }

        private static string GetStepType(object step)
        {
            switch (step)
            {
                case ExecuteStep _: return "Execute";
                case CopyFileStep _: return "CopyFile";
                case OpenInEditorStep _: return "OpenInEditor";
                case RunActionStep _: return "RunAction";
                case ReadDebugDataStep _: return "ReadDebugData";
            }
            throw new ArgumentException($"Step type identifier is not defined for {step.GetType()}", nameof(step));
        }
    }
}
