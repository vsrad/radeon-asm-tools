using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using System;
using System.ComponentModel;
using System.Threading.Tasks;
using VSRAD.Package.ProjectSystem.Macros;
using VSRAD.Package.Utils;

namespace VSRAD.Package.Options
{
    // Note: when adding a new step, don't forget to add the new type to ActionStepJsonConverter.

    public interface IActionStep : INotifyPropertyChanged
    {
        [JsonIgnore]
        StepEnvironment Environment { get; }

        Task<IActionStep> EvaluateAsync(IMacroEvaluator evaluator);
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

    public sealed class CopyFileStep : DefaultNotifyPropertyChanged, IActionStep
    {
        public StepEnvironment Environment => StepEnvironment.Remote;

        private FileCopyDirection _direction;
        public FileCopyDirection Direction { get => _direction; set => SetField(ref _direction, value); }

        private string _localPath = "";
        public string LocalPath { get => _localPath; set => SetField(ref _localPath, value); }

        private string _remotePath = "";
        public string RemotePath { get => _remotePath; set => SetField(ref _remotePath, value); }

        private bool _checkTimestamp;
        public bool CheckTimestamp { get => _checkTimestamp; set => SetField(ref _checkTimestamp, value); }

        public override string ToString() => "Copy File";

        public override bool Equals(object obj) =>
            obj is CopyFileStep step &&
            LocalPath == step.LocalPath &&
            RemotePath == step.RemotePath &&
            CheckTimestamp == step.CheckTimestamp;

        public override int GetHashCode() =>
            (LocalPath, RemotePath, CheckTimestamp).GetHashCode();

        public async Task<IActionStep> EvaluateAsync(IMacroEvaluator evaluator) =>
            new CopyFileStep
            {
                Direction = Direction,
                LocalPath = await evaluator.EvaluateAsync(LocalPath),
                RemotePath = await evaluator.EvaluateAsync(RemotePath),
                CheckTimestamp = CheckTimestamp
            };
    }

    public sealed class ExecuteStep : DefaultNotifyPropertyChanged, IActionStep
    {
        private StepEnvironment _type;
        public StepEnvironment Environment { get => _type; set => SetField(ref _type, value); }

        private string _executable = "";
        public string Executable { get => _executable; set => SetField(ref _executable, value); }

        private string _arguments = "";
        public string Arguments { get => _arguments; set => SetField(ref _arguments, value); }

        public override string ToString() => "Execute";

        public override bool Equals(object obj) =>
            obj is ExecuteStep step &&
            Environment == step.Environment &&
            Executable == step.Executable &&
            Arguments == step.Arguments;

        public override int GetHashCode() =>
            (Environment, Executable, Arguments).GetHashCode();

        public async Task<IActionStep> EvaluateAsync(IMacroEvaluator evaluator) =>
            new ExecuteStep
            {
                Environment = Environment,
                Executable = await evaluator.EvaluateAsync(Executable),
                Arguments = await evaluator.EvaluateAsync(Arguments)
            };
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
            }
            throw new ArgumentException($"Unknown step type identifer {type}", nameof(type));
        }

        private static string GetStepType(object step)
        {
            switch (step)
            {
                case ExecuteStep _: return "Execute";
                case CopyFileStep _: return "CopyFile";
            }
            throw new ArgumentException($"Step type identifier is not defined for {step.GetType()}", nameof(step));
        }
    }
}
