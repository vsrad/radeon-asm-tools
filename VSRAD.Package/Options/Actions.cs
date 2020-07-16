using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using VSRAD.Package.ProjectSystem.Macros;
using VSRAD.Package.Utils;

namespace VSRAD.Package.Options
{
    // Note: when adding a new step, don't forget to add the new type to ActionStepJsonConverter.

    // Note: steps override GetHashCode and set it to a constant value.
    // While this leads to poor performance when used as a key in hash-based collections,
    // it satisfies the contract (the same hash code is returned for objects that are equal)
    // and prevents errors when an object is mutated (e.g. in profile editor's WPF controls, https://stackoverflow.com/q/15365905)

    public interface IActionStep : INotifyPropertyChanged
    {
        [JsonIgnore]
        string Description { get; }

        Task<IActionStep> EvaluateAsync(IMacroEvaluator evaluator, ProfileOptions profile);
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum StepEnvironment
    {
        Remote, Local
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum FileCopyDirection
    {
        RemoteToLocal, LocalToRemote
    }

    public static class ActionExtensions
    {
        public static bool IsRemote(this BuiltinActionFile file) =>
            file.Location == StepEnvironment.Remote;
    }

    public sealed class CopyFileStep : DefaultNotifyPropertyChanged, IActionStep
    {
        private FileCopyDirection _direction;
        public FileCopyDirection Direction
        {
            get => _direction;
            set { SetField(ref _direction, value); RaisePropertyChanged(nameof(Description)); }
        }

        private string _sourcePath = "";
        public string SourcePath
        {
            get => _sourcePath;
            set { SetField(ref _sourcePath, value); RaisePropertyChanged(nameof(Description)); }
        }

        private string _targetPath = "";
        public string TargetPath
        {
            get => _targetPath;
            set { SetField(ref _targetPath, value); RaisePropertyChanged(nameof(Description)); }
        }

        private bool _checkTimestamp;
        public bool CheckTimestamp { get => _checkTimestamp; set => SetField(ref _checkTimestamp, value); }

        public string Description
        {
            get
            {
                if (string.IsNullOrEmpty(SourcePath) || string.IsNullOrEmpty(TargetPath))
                    return "Copy File (not configured)";

                var dir = Direction == FileCopyDirection.LocalToRemote ? "to Remote" : "from Remote";
                return $"Copy {dir} {SourcePath} -> {TargetPath}";
            }
        }

        public override bool Equals(object obj) =>
            obj is CopyFileStep step &&
            SourcePath == step.SourcePath &&
            TargetPath == step.TargetPath &&
            CheckTimestamp == step.CheckTimestamp;

        public override int GetHashCode() => 1;

        public async Task<IActionStep> EvaluateAsync(IMacroEvaluator evaluator, ProfileOptions profile) =>
            new CopyFileStep
            {
                Direction = Direction,
                SourcePath = await evaluator.EvaluateAsync(SourcePath),
                TargetPath = await evaluator.EvaluateAsync(TargetPath),
                CheckTimestamp = CheckTimestamp
            };
    }

    public sealed class ExecuteStep : DefaultNotifyPropertyChanged, IActionStep
    {
        private StepEnvironment _environment;
        public StepEnvironment Environment
        {
            get => _environment;
            set { SetField(ref _environment, value); RaisePropertyChanged(nameof(Description)); }
        }

        private string _executable = "";
        public string Executable
        {
            get => _executable;
            set { SetField(ref _executable, value); RaisePropertyChanged(nameof(Description)); }
        }

        private string _arguments = "";
        public string Arguments
        {
            get => _arguments;
            set { SetField(ref _arguments, value); RaisePropertyChanged(nameof(Description)); }
        }

        private string _workingDirectory = "";
        public string WorkingDirectory { get => _workingDirectory; set => SetField(ref _workingDirectory, value); }

        private bool _runAsAdmin;
        public bool RunAsAdmin { get => _runAsAdmin; set => SetField(ref _runAsAdmin, value); }

        private bool _waitForCompletion = true;
        public bool WaitForCompletion { get => _waitForCompletion; set => SetField(ref _waitForCompletion, value); }

        private int _timeoutSecs = 0;
        public int TimeoutSecs { get => _timeoutSecs; set => SetField(ref _timeoutSecs, value); }

        public string Description
        {
            get
            {
                if (string.IsNullOrEmpty(Executable))
                    return "Execute (not configured)";

                var env = Environment == StepEnvironment.Remote ? "Remote" : "Local";
                return $"{env} Execute {Executable} {Arguments}";
            }
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

        public async Task<IActionStep> EvaluateAsync(IMacroEvaluator evaluator, ProfileOptions profile) =>
            new ExecuteStep
            {
                Environment = Environment,
                Executable = await evaluator.EvaluateAsync(Executable),
                Arguments = await evaluator.EvaluateAsync(Arguments),
                WorkingDirectory = await evaluator.EvaluateAsync(WorkingDirectory),
                RunAsAdmin = RunAsAdmin,
                WaitForCompletion = WaitForCompletion,
                TimeoutSecs = TimeoutSecs
            };
    }

    public sealed class OpenInEditorStep : DefaultNotifyPropertyChanged, IActionStep
    {
        private string _path = "";
        public string Path
        {
            get => _path;
            set
            {
                SetField(ref _path, value);
                RaisePropertyChanged(nameof(Description));
            }
        }

        private string _lineMarker = "";
        public string LineMarker { get => _lineMarker; set => SetField(ref _lineMarker, value); }

        public string Description
        {
            get
            {
                if (string.IsNullOrEmpty(Path))
                    return "Open in Editor (not configured)";
                return $"Open {Path}";
            }
        }

        public override bool Equals(object obj) =>
            obj is OpenInEditorStep step && Path == step.Path && LineMarker == step.LineMarker;

        public override int GetHashCode() => 5;

        public async Task<IActionStep> EvaluateAsync(IMacroEvaluator evaluator, ProfileOptions profile) =>
            new OpenInEditorStep
            {
                Path = await evaluator.EvaluateAsync(Path),
                LineMarker = LineMarker
            };
    }

    public sealed class RunActionStep : DefaultNotifyPropertyChanged, IActionStep
    {
        private string _name = "";
        public string Name
        {
            get => _name;
            set
            {
                SetField(ref _name, value);
                RaisePropertyChanged(nameof(Description));
            }
        }

        [JsonIgnore]
        public List<IActionStep> EvaluatedSteps { get; }

        public RunActionStep() : this(null) { }

        public RunActionStep(List<IActionStep> evaluatedSteps)
        {
            EvaluatedSteps = evaluatedSteps;
        }

        public string Description
        {
            get
            {
                if (string.IsNullOrEmpty(Name))
                    return "Run Action (not configured)";
                return $"Run {Name}";
            }
        }

        public override bool Equals(object obj) => obj is RunActionStep step && Name == step.Name;

        public override int GetHashCode() => 7;

        public Task<IActionStep> EvaluateAsync(IMacroEvaluator evaluator, ProfileOptions profile) =>
            EvaluateAsync(evaluator, profile, new Stack<string>());

        public async Task<IActionStep> EvaluateAsync(IMacroEvaluator evaluator, ProfileOptions profile, Stack<string> callers)
        {
            if (callers.Contains(Name))
                throw new Exception("Encountered a circular action: " + string.Join(" -> ", callers.Reverse()) + " -> " + Name);

            var action = profile.Actions.FirstOrDefault(a => a.Name == Name);
            if (action == null)
                throw new Exception("Action " + Name + " not found" + (callers.Count == 0 ? "" : ", required by " + string.Join(" -> ", callers.Reverse()) + " -> " + Name));

            callers.Push(Name);

            var evaluatedSteps = new List<IActionStep>();
            foreach (var step in action.Steps)
            {
                IActionStep evaluated;
                if (step is RunActionStep runAction)
                    evaluated = await runAction.EvaluateAsync(evaluator, profile, callers);
                else
                    evaluated = await step.EvaluateAsync(evaluator, profile);
                evaluatedSteps.Add(evaluated);
            }

            callers.Pop();

            return new RunActionStep(evaluatedSteps) { Name = Name };
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
                case "Execute": return new ExecuteStep();
                case "CopyFile": return new CopyFileStep();
                case "OpenInEditor": return new OpenInEditorStep();
                case "RunAction": return new RunActionStep();
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
            }
            throw new ArgumentException($"Step type identifier is not defined for {step.GetType()}", nameof(step));
        }
    }
}
