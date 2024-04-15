using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
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

    public sealed class ActionEvaluationTransients
    {
        public string LocalWorkDir { get; }
        public string RemoteWorkDir { get; }
        public bool RunActionsLocally { get; }
        public OSPlatform ServerPlatform { get; }
        public IEnumerable<ActionProfileOptions> Actions { get; }

        public ActionEvaluationTransients(string localWorkDir, string remoteWorkDir, bool runActionsLocally, OSPlatform serverPlatform, IEnumerable<ActionProfileOptions> actions)
        {
            LocalWorkDir = localWorkDir;
            RemoteWorkDir = remoteWorkDir;
            RunActionsLocally = runActionsLocally;
            ServerPlatform = serverPlatform;
            Actions = actions;
        }

        public Result<string> ResolveFullPath(string path, StepEnvironment location)
        {
            try
            {
                if (location == StepEnvironment.Local)
                    return Path.Combine(LocalWorkDir, path);
                if (ServerPlatform == OSPlatform.Windows)
                    return Path.Combine(RemoteWorkDir, path);
                /* Else handle UNIX server paths */
                if (RemoteWorkDir.Length == 0 || path.StartsWith("/", StringComparison.Ordinal))
                    return path;
                else if (RemoteWorkDir.EndsWith("/", StringComparison.Ordinal))
                    return RemoteWorkDir + path;
                else
                    return RemoteWorkDir + '/' + path;
            }
            catch (ArgumentException e) when (e.Message == "Illegal characters in path.")
            {
                var workDir = location == StepEnvironment.Remote ? RemoteWorkDir : LocalWorkDir;
                return new Error($"Path contains illegal characters: \"{path}\"\r\nWorking directory: \"{workDir}\"");
            }
        }
    }

    public interface IActionStep : INotifyPropertyChanged
    {
        Task<Result<IActionStep>> EvaluateAsync(IMacroEvaluator evaluator, ActionEvaluationTransients transients, string sourceAction);
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum StepEnvironment
    {
        Remote, Local
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum CopyDirection
    {
        RemoteToLocal, LocalToRemote, LocalToLocal
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum ActionCondition
    {
        Always, IfEqual, IfNotEqual
    }

    public static class ActionExtensions
    {
        public static bool IsRemote(this BuiltinActionFile file) =>
            file.Location == StepEnvironment.Remote;

        public static Error EvaluationError(string action, string step, string description) =>
            new Error(description, title: $"{step} step failed in \"{action}\"");

        public static Error NestedEvaluationError(string parentAction, Error childError) =>
            new Error(childError.Message, title: childError.Title + " <- " + $"\"{parentAction}\"");
    }

    public sealed class CopyStep : DefaultNotifyPropertyChanged, IActionStep
    {
        private CopyDirection _direction;
        public CopyDirection Direction { get => _direction; set => SetField(ref _direction, value); }

        private string _sourcePath = "";
        public string SourcePath { get => _sourcePath; set => SetField(ref _sourcePath, value); }

        private string _targetPath = "";
        public string TargetPath { get => _targetPath; set => SetField(ref _targetPath, value); }

        private string _globsToCopy = "*";
        public string GlobsToCopy { get => _globsToCopy; set => SetField(ref _globsToCopy, value); }

        private bool _skipIfNotModified;
        public bool SkipIfNotModified { get => _skipIfNotModified; set => SetField(ref _skipIfNotModified, value); }

        private bool _preserveTimestamps;
        public bool PreserveTimestamps { get => _preserveTimestamps; set => SetField(ref _preserveTimestamps, value); }

        private bool _useCompression;
        public bool UseCompression { get => _useCompression; set => SetField(ref _useCompression, value); }

        [JsonIgnore]
        public string[] GlobsToCopyArray => GlobsToCopy.Split(';');

        public override string ToString()
        {
            if (string.IsNullOrWhiteSpace(SourcePath) || string.IsNullOrWhiteSpace(TargetPath))
                return "Copy";

            var direction = Direction == CopyDirection.LocalToRemote ? "to Remote"
                          : Direction == CopyDirection.RemoteToLocal ? "from Remote"
                          : "Local";

            return $"Copy {direction} {GlobsToCopy}";
        }

        public override bool Equals(object obj) =>
            obj is CopyStep step &&
            SourcePath == step.SourcePath &&
            TargetPath == step.TargetPath &&
            GlobsToCopy == step.GlobsToCopy &&
            SkipIfNotModified == step.SkipIfNotModified &&
            PreserveTimestamps == step.PreserveTimestamps &&
            UseCompression == step.UseCompression;

        public override int GetHashCode() => 1;

        public async Task<Result<IActionStep>> EvaluateAsync(IMacroEvaluator evaluator, ActionEvaluationTransients transients, string sourceAction)
        {
            var sourcePathResult = await evaluator.EvaluateAsync(SourcePath);
            if (!sourcePathResult.TryGetResult(out var evaluatedSourcePath, out var error))
                return EvaluationError(sourceAction, "Copy", error.Message);
            var targetPathResult = await evaluator.EvaluateAsync(TargetPath);
            if (!targetPathResult.TryGetResult(out var evaluatedTargetPath, out error))
                return EvaluationError(sourceAction, "Copy", error.Message);
            var globsToCopyResult = await evaluator.EvaluateAsync(GlobsToCopy);
            if (!globsToCopyResult.TryGetResult(out var evaluatedGlobsToCopy, out error))
                return EvaluationError(sourceAction, "Copy", error.Message);

            if (string.IsNullOrWhiteSpace(evaluatedSourcePath))
                return EvaluationError(sourceAction, "Copy", "The source path evaluates to an empty string");
            if (string.IsNullOrWhiteSpace(evaluatedTargetPath))
                return EvaluationError(sourceAction, "Copy", "The target path evaluates to an empty string");

            var direction = transients.RunActionsLocally ? CopyDirection.LocalToLocal : Direction;

            var sourceLocation = direction == CopyDirection.RemoteToLocal ? StepEnvironment.Remote : StepEnvironment.Local;
            var targetLocation = direction == CopyDirection.LocalToRemote ? StepEnvironment.Remote : StepEnvironment.Local;
            if (!transients.ResolveFullPath(evaluatedSourcePath, sourceLocation).TryGetResult(out evaluatedSourcePath, out error))
                return EvaluationError(sourceAction, "Copy", error.Message);
            if (!transients.ResolveFullPath(evaluatedTargetPath, targetLocation).TryGetResult(out evaluatedTargetPath, out error))
                return EvaluationError(sourceAction, "Copy", error.Message);

            return new CopyStep
            {
                Direction = direction,
                SourcePath = evaluatedSourcePath,
                TargetPath = evaluatedTargetPath,
                GlobsToCopy = evaluatedGlobsToCopy,
                SkipIfNotModified = SkipIfNotModified,
                PreserveTimestamps = PreserveTimestamps,
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
            TimeoutSecs == step.TimeoutSecs;

        public override int GetHashCode() => 3;

        public async Task<Result<IActionStep>> EvaluateAsync(IMacroEvaluator evaluator, ActionEvaluationTransients transients, string sourceAction)
        {
            if (string.IsNullOrWhiteSpace(Executable))
                return EvaluationError(sourceAction, "Execute", "No executable specified");

            var executableResult = await evaluator.EvaluateAsync(Executable);
            if (!executableResult.TryGetResult(out var evaluatedExecutable, out var error))
                return EvaluationError(sourceAction, "Execute", error.Message);
            var argumentsResult = await evaluator.EvaluateAsync(Arguments);
            if (!argumentsResult.TryGetResult(out var evaluatedArguments, out error))
                return EvaluationError(sourceAction, "Execute", error.Message);
            var workdirResult = await evaluator.EvaluateAsync(WorkingDirectory);
            if (!workdirResult.TryGetResult(out var evaluatedWorkdir, out error))
                return EvaluationError(sourceAction, "Execute", error.Message);

            if (string.IsNullOrWhiteSpace(evaluatedExecutable))
                return EvaluationError(sourceAction, "Execute", $"The specified executable (\"{Executable}\") evaluates to an empty string");

            var environment = transients.RunActionsLocally ? StepEnvironment.Local : Environment;

            if (string.IsNullOrWhiteSpace(evaluatedWorkdir))
            {
                if (environment == StepEnvironment.Remote)
                    evaluatedWorkdir = transients.RemoteWorkDir;
                else
                    evaluatedWorkdir = transients.LocalWorkDir;
            }

            return new ExecuteStep
            {
                Environment = environment,
                Executable = evaluatedExecutable,
                Arguments = evaluatedArguments,
                WorkingDirectory = evaluatedWorkdir,
                RunAsAdmin = RunAsAdmin,
                WaitForCompletion = WaitForCompletion,
                TimeoutSecs = TimeoutSecs
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

        public async Task<Result<IActionStep>> EvaluateAsync(IMacroEvaluator evaluator, ActionEvaluationTransients transients, string sourceAction)
        {
            if (string.IsNullOrWhiteSpace(Path))
                return EvaluationError(sourceAction, "Open in Editor", "No file path specified");

            var pathResult = await evaluator.EvaluateAsync(Path);
            if (!pathResult.TryGetResult(out var evaluatedPath, out var error))
                return EvaluationError(sourceAction, "Open in Editor", error.Message);

            if (string.IsNullOrWhiteSpace(evaluatedPath))
                return EvaluationError(sourceAction, "Open in Editor", $"The specified file path (\"{Path}\") evaluates to an empty string");

            if (!transients.ResolveFullPath(evaluatedPath, StepEnvironment.Local).TryGetResult(out evaluatedPath, out error))
                return EvaluationError(sourceAction, "Open in Editor", error.Message);

            return new OpenInEditorStep { Path = evaluatedPath, LineMarker = LineMarker };
        }
    }

    public sealed class RunActionStep : DefaultNotifyPropertyChanged, IActionStep
    {
        private string _name = "";
        public string Name { get => _name; set => SetField(ref _name, value); }

        private ActionCondition _condition;
        public ActionCondition Condition { get => _condition; set => SetField(ref _condition, value); }

        private string _conditionLhs = "";
        public string ConditionLhs { get => _conditionLhs; set => SetField(ref _conditionLhs, value); }

        private string _conditionRhs = "";
        public string ConditionRhs { get => _conditionRhs; set => SetField(ref _conditionRhs, value); }

        [JsonIgnore]
        public bool EvaluatedCondition { get; }

        [JsonIgnore]
        public List<IActionStep> EvaluatedSteps { get; }

        public RunActionStep() : this(false, null) { }

        public RunActionStep(bool evaluatedCondition, List<IActionStep> evaluatedSteps)
        {
            EvaluatedCondition = evaluatedCondition;
            EvaluatedSteps = evaluatedSteps;
        }

        public override string ToString() =>
            string.IsNullOrWhiteSpace(Name) ? "Run Action" : $"Run {Name}" + (Condition == ActionCondition.Always ? "" : " (Conditional)");

        public override bool Equals(object obj) =>
            obj is RunActionStep step &&
            Name == step.Name &&
            Condition == step.Condition &&
            ConditionLhs == step.ConditionLhs &&
            ConditionRhs == step.ConditionRhs;

        public override int GetHashCode() => 7;

        public Task<Result<IActionStep>> EvaluateAsync(IMacroEvaluator evaluator, ActionEvaluationTransients transients, string sourceAction) =>
            EvaluateAsync(evaluator, transients, actionStack: new[] { sourceAction });

        public async Task<Result<IActionStep>> EvaluateAsync(IMacroEvaluator evaluator, ActionEvaluationTransients transients, IEnumerable<string> actionStack)
        {
            if (string.IsNullOrWhiteSpace(Name))
                return EvaluationError(actionStack.Last(), "Run Action", "No action specified");
            if (actionStack.Contains(Name))
                return NestedEvaluationError(actionStack.Last(), EvaluationError(Name, "Run Action", "Circular dependency between actions"));

            var action = transients.Actions.FirstOrDefault(a => a.Name == Name);
            if (action == null)
                return EvaluationError(actionStack.Last(), "Run Action", $"Action \"{Name}\" is not found");

            var condLhsResult = await evaluator.EvaluateAsync(ConditionLhs);
            if (!condLhsResult.TryGetResult(out var evaluatedCondLhs, out var error))
                return EvaluationError(actionStack.Last(), "Run Action", error.Message);
            var condRhsResult = await evaluator.EvaluateAsync(ConditionRhs);
            if (!condRhsResult.TryGetResult(out var evaluatedCondRhs, out error))
                return EvaluationError(actionStack.Last(), "Run Action", error.Message);

            bool evaluatedCondition;
            if (Condition == ActionCondition.Always)
                evaluatedCondition = true;
            else if (Condition == ActionCondition.IfEqual)
                evaluatedCondition = string.Equals(evaluatedCondLhs, evaluatedCondRhs, StringComparison.Ordinal);
            else if (Condition == ActionCondition.IfNotEqual)
                evaluatedCondition = !string.Equals(evaluatedCondLhs, evaluatedCondRhs, StringComparison.Ordinal);
            else
                throw new NotImplementedException();

            var evaluatedSteps = new List<IActionStep>();
            foreach (var step in action.Steps)
            {
                Result<IActionStep> evalResult;
                if (step is RunActionStep runAction)
                    evalResult = await runAction.EvaluateAsync(evaluator, transients, actionStack.Append(Name));
                else
                    evalResult = await step.EvaluateAsync(evaluator, transients, Name);
                if (!evalResult.TryGetResult(out var evaluated, out error))
                    return NestedEvaluationError(actionStack.Last(), error);

                evaluatedSteps.Add(evaluated);
            }
            return new RunActionStep(evaluatedCondition, evaluatedSteps) { Name = Name, Condition = Condition, ConditionLhs = evaluatedCondLhs, ConditionRhs = evaluatedCondRhs };
        }
    }

    public sealed class WriteDebugTargetStep : DefaultNotifyPropertyChanged, IActionStep
    {
        private string _breakpointListPath = "";
        public string BreakpointListPath { get => _breakpointListPath; set => SetField(ref _breakpointListPath, value); }

        private string _watchListPath = "";
        public string WatchListPath { get => _watchListPath; set => SetField(ref _watchListPath, value); }

        public override string ToString() =>
            string.IsNullOrWhiteSpace(BreakpointListPath) ? "Write Debug Target (Breakpoint List) (Watch List)" : $"Write Debug Target (Breakpoint List {BreakpointListPath}) (Watch List {WatchListPath})";

        public override bool Equals(object obj) =>
            obj is WriteDebugTargetStep step && BreakpointListPath == step.BreakpointListPath && WatchListPath == step.WatchListPath;

        public override int GetHashCode() => 9;

        public async Task<Result<IActionStep>> EvaluateAsync(IMacroEvaluator evaluator, ActionEvaluationTransients transients, string sourceAction)
        {
            var breakpointsResult = await evaluator.EvaluateAsync(BreakpointListPath);
            if (!breakpointsResult.TryGetResult(out var breakpointsPath, out var error))
                return EvaluationError(sourceAction, "Write Debug Target", error.Message);
            if (string.IsNullOrEmpty(breakpointsPath))
                return EvaluationError(sourceAction, "Write Debug Target", "Breakpoint list path is not specified or evaluates to an empty string");
            if (!transients.ResolveFullPath(breakpointsPath, StepEnvironment.Local).TryGetResult(out breakpointsPath, out error))
                return EvaluationError(sourceAction, "Write Debug Target", error.Message);

            var watchesResult = await evaluator.EvaluateAsync(WatchListPath);
            if (!watchesResult.TryGetResult(out var watchesPath, out error))
                return EvaluationError(sourceAction, "Write Debug Target", error.Message);
            if (string.IsNullOrEmpty(watchesPath))
                return EvaluationError(sourceAction, "Write Debug Target", "Watch list path is not specified or evaluates to an empty string");
            if (!transients.ResolveFullPath(watchesPath, StepEnvironment.Local).TryGetResult(out watchesPath, out error))
                return EvaluationError(sourceAction, "Write Debug Target", error.Message);

            return new WriteDebugTargetStep { BreakpointListPath = breakpointsPath, WatchListPath = watchesPath };
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

        private uint? _checkMagicNumber = 0x77777777; // Default value, do not change
        [JsonConverter(typeof(JsonMagicNumberConverter))]
        public uint? CheckMagicNumber { get => _checkMagicNumber; set => SetField(ref _checkMagicNumber, value); }

        public ReadDebugDataStep() : this(new BuiltinActionFile(), new BuiltinActionFile(), new BuiltinActionFile(), binaryOutput: true, outputOffset: 0, magicNumber: null) { }

        public ReadDebugDataStep(BuiltinActionFile outputFile, BuiltinActionFile watchesFile, BuiltinActionFile dispatchParamsFile, bool binaryOutput, int outputOffset, uint? magicNumber)
        {
            OutputFile = outputFile;
            WatchesFile = watchesFile;
            DispatchParamsFile = dispatchParamsFile;
            BinaryOutput = binaryOutput;
            OutputOffset = outputOffset;
            CheckMagicNumber = magicNumber;
        }

        public async Task<Result<IActionStep>> EvaluateAsync(IMacroEvaluator evaluator, ActionEvaluationTransients transients, string sourceAction)
        {
            var outputResult = await OutputFile.EvaluateAsync(evaluator);
            if (!outputResult.TryGetResult(out var outputFile, out var error))
                return EvaluationError(sourceAction, "Read Debug Data", error.Message);
            if (string.IsNullOrEmpty(outputFile.Path))
                return EvaluationError(sourceAction, "Read Debug Data", "Debug data path is not specified");

            var watchesResult = await WatchesFile.EvaluateAsync(evaluator);
            if (!watchesResult.TryGetResult(out var watchesFile, out error))
                return EvaluationError(sourceAction, "Read Debug Data", error.Message);
            if (string.IsNullOrEmpty(watchesFile.Path))
                return EvaluationError(sourceAction, "Read Debug Data", "Valid watches path is not specified");

            var dispatchParamsResult = await DispatchParamsFile.EvaluateAsync(evaluator);
            if (!dispatchParamsResult.TryGetResult(out var dispatchParamsFile, out error))
                return EvaluationError(sourceAction, "Read Debug Data", error.Message);
            if (string.IsNullOrEmpty(dispatchParamsFile.Path))
                return EvaluationError(sourceAction, "Read Debug Data", "Dispatch parameters path is not specified");

            if (transients.RunActionsLocally)
            {
                outputFile.Location = StepEnvironment.Local;
                watchesFile.Location = StepEnvironment.Local;
                dispatchParamsFile.Location = StepEnvironment.Local;
            }

            if (!transients.ResolveFullPath(outputFile.Path, outputFile.Location).TryGetResult(out var outputFullPath, out error))
                return EvaluationError(sourceAction, "Read Debug Data", error.Message);
            outputFile.Path = outputFullPath;

            if (!transients.ResolveFullPath(watchesFile.Path, watchesFile.Location).TryGetResult(out var watchesFullPath, out error))
                return EvaluationError(sourceAction, "Read Debug Data", error.Message);
            watchesFile.Path = watchesFullPath;

            if (!transients.ResolveFullPath(dispatchParamsFile.Path, dispatchParamsFile.Location).TryGetResult(out var dispatchParamsFullPath, out error))
                return EvaluationError(sourceAction, "Read Debug Data", error.Message);
            dispatchParamsFile.Path = dispatchParamsFullPath;

            return new ReadDebugDataStep(outputOffset: OutputOffset, binaryOutput: BinaryOutput, magicNumber: CheckMagicNumber,
                outputFile: outputFile, watchesFile: watchesFile, dispatchParamsFile: dispatchParamsFile);
        }

        public override string ToString()
        {
            if (string.IsNullOrWhiteSpace(OutputFile.Path))
                return "Read Debug Data";

            var env = OutputFile.Location == StepEnvironment.Remote ? "Remote" : "Local";
            return $"Read Debug Data {env} {OutputFile.Path}";
        }

        public override int GetHashCode() => 11;

        public override bool Equals(object obj) =>
            obj is ReadDebugDataStep step &&
            OutputFile.Equals(step.OutputFile) &&
            WatchesFile.Equals(step.WatchesFile) &&
            DispatchParamsFile.Equals(step.DispatchParamsFile) &&
            BinaryOutput == step.BinaryOutput &&
            OutputOffset == step.OutputOffset &&
            CheckMagicNumber == step.CheckMagicNumber;
    }

    public sealed class VerifyFileModifiedStep : DefaultNotifyPropertyChanged, IActionStep
    {
        private StepEnvironment _location = StepEnvironment.Local;
        public StepEnvironment Location { get => _location; set => SetField(ref _location, value); }

        private string _path = "";
        public string Path { get => _path; set => SetField(ref _path, value); }

        private bool _abortIfNotModified;
        public bool AbortIfNotModifed { get => _abortIfNotModified; set => SetField(ref _abortIfNotModified, value); }

        private string _errorMessage = "";
        public string ErrorMessage { get => _errorMessage; set => SetField(ref _errorMessage, value); }

        public override string ToString() =>
            string.IsNullOrWhiteSpace(Path) ? "Verify File Modified" : $"Verify File Modified {Location} {Path}";

        public override bool Equals(object obj) =>
            obj is VerifyFileModifiedStep step && Path == step.Path && AbortIfNotModifed == step.AbortIfNotModifed && ErrorMessage == step.ErrorMessage;

        public override int GetHashCode() => 13;

        public async Task<Result<IActionStep>> EvaluateAsync(IMacroEvaluator evaluator, ActionEvaluationTransients transients, string sourceAction)
        {
            var pathResult = await evaluator.EvaluateAsync(Path);
            if (!pathResult.TryGetResult(out var evaluatedPath, out var error))
                return EvaluationError(sourceAction, "Verify File Modified", error.Message);
            var messageResult = await evaluator.EvaluateAsync(ErrorMessage);
            if (!messageResult.TryGetResult(out var evaluatedMessage, out error))
                return EvaluationError(sourceAction, "Verify File Modified", error.Message);

            if (string.IsNullOrWhiteSpace(evaluatedPath))
                return EvaluationError(sourceAction, "Verify File Modified", "The path evaluates to an empty string");
            if (!transients.ResolveFullPath(evaluatedPath, Location).TryGetResult(out evaluatedPath, out error))
                return EvaluationError(sourceAction, "Verify File Modified", error.Message);

            if (string.IsNullOrWhiteSpace(evaluatedMessage))
                return EvaluationError(sourceAction, "Verify File Modified", "The error message evaluates to an empty string");

            return new VerifyFileModifiedStep { Location = Location, Path = evaluatedPath, AbortIfNotModifed = AbortIfNotModifed, ErrorMessage = evaluatedMessage };
        }
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
                case "Copy": return new CopyStep();
                case "Execute": return new ExecuteStep();
                case "OpenInEditor": return new OpenInEditorStep();
                case "RunAction": return new RunActionStep();
                case "WriteDebugTarget": return new WriteDebugTargetStep();
                case "ReadDebugData": return new ReadDebugDataStep();
                case "VerifyFileModified": return new VerifyFileModifiedStep();
            }
            throw new InvalidDataException($"Unknown step type identifer \"{type}\"");
        }

        private static string GetStepType(object step)
        {
            switch (step)
            {
                case CopyStep _: return "Copy";
                case ExecuteStep _: return "Execute";
                case OpenInEditorStep _: return "OpenInEditor";
                case RunActionStep _: return "RunAction";
                case WriteDebugTargetStep _: return "WriteDebugTarget";
                case ReadDebugDataStep _: return "ReadDebugData";
                case VerifyFileModifiedStep _: return "VerifyFileModified";
            }
            throw new InvalidDataException($"Step type identifier is not defined for {step.GetType()}");
        }
    }
}
