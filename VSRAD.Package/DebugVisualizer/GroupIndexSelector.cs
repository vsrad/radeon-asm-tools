using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using VSRAD.Package.Server;

namespace VSRAD.Package.DebugVisualizer
{
    public class GroupIndexChangedEventArgs : EventArgs
    {
        public string Coordinates { get; }
        public uint GroupIndex { get; }
        public uint GroupSize { get; }
        public bool IsValid { get; set; } = true;
        public uint DataGroupCount { get; set; }

        public GroupIndexChangedEventArgs(string coordinates, uint groupIndex, uint groupSize)
        {
            Coordinates = coordinates;
            GroupIndex = groupIndex;
            GroupSize = groupSize;
        }
    }

    public sealed class GroupIndexSelector : INotifyPropertyChanged, INotifyDataErrorInfo
    {
        public event PropertyChangedEventHandler PropertyChanged;
        public event EventHandler<DataErrorsChangedEventArgs> ErrorsChanged;

        public event EventHandler<GroupIndexChangedEventArgs> IndexChanged;

        private uint _x;
        public uint X { get => _x; set => SetField(ref _x, value); }

        private uint _y;
        public uint Y { get => _y; set => SetField(ref _y, value); }

        private uint _z;
        public uint Z { get => _z; set => SetField(ref _z, value); }

        private uint _dimX = 1;
        public uint DimX { get => _dimX; set { SetField(ref _dimX, value); RaisePropertyChanged(nameof(MaximumX)); } }

        private uint _dimY = 1;
        public uint DimY { get => _dimY; set { SetField(ref _dimY, value); RaisePropertyChanged(nameof(MaximumY)); } }

        private uint _dimZ = 1;
        public uint DimZ { get => _dimZ; set { SetField(ref _dimZ, value); RaisePropertyChanged(nameof(MaximumZ)); } }

        // OneWay bindings in XAML do not work on these properties for some reason, hence the empty setters
        public uint MaximumX { get => _projectOptions.VisualizerOptions.NDRange3D ? DimX - 1 : uint.MaxValue; set { } }
        public uint MaximumY { get => DimY - 1; set { } }
        public uint MaximumZ { get => DimZ - 1; set { } }

        private string _error;
        public bool HasErrors => _error != null;

        private bool _updateOptions = true;
        private BreakStateDispatchParameters _currentDispatchParams;

        private readonly Options.ProjectOptions _projectOptions;

        public GroupIndexSelector(Options.ProjectOptions options)
        {
            _projectOptions = options;
            PropertyChangedEventManager.AddHandler(_projectOptions.VisualizerOptions, OptionsChanged, "");
            PropertyChangedEventManager.AddHandler(_projectOptions.DebuggerOptions, OptionsChanged, "");
        }

        private void OptionsChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(Options.VisualizerOptions.ManualMode):
                    if (!_projectOptions.VisualizerOptions.ManualMode && _currentDispatchParams != null)
                        SetOptionsFromDispatchParams();
                    break;
                case nameof(Options.VisualizerOptions.NDRange3D):
                    RaisePropertyChanged(nameof(MaximumX));
                    if (_updateOptions) Update();
                    break;
                case nameof(Options.DebuggerOptions.WaveSize):
                case nameof(Options.DebuggerOptions.GroupSize):
                case nameof(Options.DebuggerOptions.NGroups):
                    if (_updateOptions) Update();
                    break;
            }
        }

        public void UpdateOnBreak(BreakState breakState)
        {
            _currentDispatchParams = breakState.DispatchParameters;
            if (!_projectOptions.VisualizerOptions.ManualMode && _currentDispatchParams != null)
                SetOptionsFromDispatchParams();
            else
                Update();
        }

        private void SetOptionsFromDispatchParams()
        {
            _updateOptions = false;

            _projectOptions.VisualizerOptions.NDRange3D = _currentDispatchParams.NDRange3D;
            _projectOptions.DebuggerOptions.WaveSize = _currentDispatchParams.WaveSize;

            DimX = _currentDispatchParams.DimX;
            DimY = _currentDispatchParams.DimY;
            DimZ = _currentDispatchParams.DimZ;

            _projectOptions.DebuggerOptions.NGroups = _currentDispatchParams.NDRange3D
                ? _currentDispatchParams.DimX * _currentDispatchParams.DimY * _currentDispatchParams.DimZ
                : _currentDispatchParams.DimX;

            if (_currentDispatchParams.GroupSizeX > 8192)
                Errors.ShowWarning("Group Size from dispatch params exceeded the limit - 8192. Proceeding with 8192.");

            _projectOptions.DebuggerOptions.GroupSize = Math.Min(_currentDispatchParams.GroupSizeX, 8192);

            _updateOptions = true;
            Update();
        }

        public void GoToGroup(uint groupIdx)
        {
            if (_projectOptions.VisualizerOptions.NDRange3D)
            {
                _updateOptions = false;
                X = groupIdx % DimX;
                groupIdx /= DimX;
                Y = groupIdx % DimY;
                groupIdx /= DimY;
                Z = groupIdx;
                _updateOptions = true;
                Update();
            }
            else
            {
                X = groupIdx;
            }
        }

        public void Update()
        {
            var index = _projectOptions.VisualizerOptions.NDRange3D ? (X + Y * DimX + Z * DimX * DimY) : X;
            var coordinates = _projectOptions.VisualizerOptions.NDRange3D ? $"({X}; {Y}; {Z})" : $"({X})";
            var args = new GroupIndexChangedEventArgs(coordinates, index, _projectOptions.DebuggerOptions.GroupSize);
            IndexChanged?.Invoke(this, args);

            _error = args.IsValid ? null : $"Invalid group index: {index} >= {args.DataGroupCount}";

            ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(nameof(X)));
            ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(nameof(Y)));
            ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(nameof(Z)));
        }

        public IEnumerable GetErrors(string propertyName)
        {
            if ((propertyName != "X" && propertyName != "Y" && propertyName != "Z")
                || _error == null)
                return Enumerable.Empty<object>();
            return new[] { _error };
        }

        private bool SetField<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
                return false;

            field = value;

            if (_updateOptions) Update();
            RaisePropertyChanged(propertyName);

            return true;
        }

        private void RaisePropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
