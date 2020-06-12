using System.ComponentModel;
using VSRAD.Package.Utils;

namespace VSRAD.Package.Options
{
    public interface IAction : INotifyPropertyChanged
    {
        ActionType Type { get; }
    }

    public enum ActionType
    {
        Local, Remote
    }

    public enum FileCopyDirection
    {
        LocalToRemote, RemoteToLocal
    }

    public sealed class CopyFileAction : DefaultNotifyPropertyChanged, IAction
    {
        public ActionType Type => ActionType.Remote;

        private FileCopyDirection _direction;
        public FileCopyDirection Direction { get => _direction; set => SetField(ref _direction, value); }

        private string _localPath;
        public string LocalPath { get => _localPath; set => SetField(ref _localPath, value); }

        private string _remotePath;
        public string RemotePath { get => _remotePath; set => SetField(ref _remotePath, value); }

        private bool _checkTimestamp;
        public bool CheckTimestamp { get => _checkTimestamp; set => SetField(ref _checkTimestamp, value); }

        public override string ToString() => "CopyFileAction";
    }

    public sealed class ExecuteAction : DefaultNotifyPropertyChanged, IAction
    {
        private ActionType _type;
        public ActionType Type { get => _type; set => SetField(ref _type, value); }

        private string _executable;
        public string Executable { get => _executable; set => SetField(ref _executable, value); }

        private string _arguments;
        public string Arguments { get => _arguments; set => SetField(ref _arguments, value); }
    }
}
