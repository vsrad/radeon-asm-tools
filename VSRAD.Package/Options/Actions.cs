using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using System;
using System.ComponentModel;
using VSRAD.Package.Utils;

namespace VSRAD.Package.Options
{
    // Note: when adding a new action, don't forget to add the new type to ActionJsonConverter.

    public interface IAction : INotifyPropertyChanged
    {
        [JsonIgnore]
        ActionEnvironment Environment { get; }
    }

    public enum ActionEnvironment
    {
        Local, Remote
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum FileCopyDirection
    {
        LocalToRemote, RemoteToLocal
    }

    public sealed class CopyFileAction : DefaultNotifyPropertyChanged, IAction
    {
        public ActionEnvironment Environment => ActionEnvironment.Remote;

        private FileCopyDirection _direction;
        public FileCopyDirection Direction { get => _direction; set => SetField(ref _direction, value); }

        private string _localPath = "";
        public string LocalPath { get => _localPath; set => SetField(ref _localPath, value); }

        private string _remotePath = "";
        public string RemotePath { get => _remotePath; set => SetField(ref _remotePath, value); }

        private bool _checkTimestamp;
        public bool CheckTimestamp { get => _checkTimestamp; set => SetField(ref _checkTimestamp, value); }

        public override string ToString() => "CopyFileAction";
    }

    public sealed class ExecuteAction : DefaultNotifyPropertyChanged, IAction
    {
        private ActionEnvironment _type;
        public ActionEnvironment Environment { get => _type; set => SetField(ref _type, value); }

        private string _executable = "";
        public string Executable { get => _executable; set => SetField(ref _executable, value); }

        private string _arguments = "";
        public string Arguments { get => _arguments; set => SetField(ref _arguments, value); }
    }

    public sealed class ActionJsonConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType) =>
            typeof(IAction).IsAssignableFrom(objectType);

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            var obj = JObject.Load(reader);
            var action = InstantiateActionFromTypeField((string)obj["Type"]);
            serializer.Populate(obj.CreateReader(), action);
            return action;
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            var serialized = JObject.FromObject(value);
            serialized["Type"] = GetActionType(value);
            serialized.WriteTo(writer);
        }

        private static IAction InstantiateActionFromTypeField(string type)
        {
            switch (type)
            {
                case "Execute": return new ExecuteAction();
                case "CopyFile": return new CopyFileAction();
            }
            throw new ArgumentException($"Unknown action type identifer {type}", nameof(type));
        }

        private static string GetActionType(object action)
        {
            switch (action)
            {
                case ExecuteAction _: return "Execute";
                case CopyFileAction _: return "CopyFile";
            }
            throw new ArgumentException($"Action type identifier is not defined for {action.GetType()}", nameof(action));
        }
    }
}
