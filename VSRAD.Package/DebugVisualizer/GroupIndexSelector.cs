﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace VSRAD.Package.DebugVisualizer
{
    public delegate uint CalculateGroupCount(uint groupSize);
    public delegate void GroupSelectionChange(uint groupIndex, string coordinates);

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
        public uint X { get => _x; set => SetField(ref _x, LimitIndex(value, _dimX)); }

        private uint _y;
        public uint Y { get => _y; set => SetField(ref _y, LimitIndex(value, _dimY)); }

        private uint _z;
        public uint Z { get => _z; set => SetField(ref _z, LimitIndex(value, _dimZ)); }

        private uint _dimX = 1;
        public uint DimX { get => _dimX; set { SetField(ref _dimX, value); X = X; } }

        private uint _dimY = 1;
        public uint DimY { get => _dimY; set { SetField(ref _dimY, value); Y = Y; } }

        private uint _dimZ = 1;
        public uint DimZ { get => _dimZ; set { SetField(ref _dimZ, value); Z = Z; } }

        private uint _groupSize = 512;
        public uint GroupSize { get => _groupSize; set { SetField(ref _groupSize, value); } }

        private string _error;
        public bool HasErrors => _error != null;

        private bool _dataAvailable = false;

        private readonly Options.VisualizerOptions _visualizerOptions;
        private readonly CalculateGroupCount _getGroupCount;
        private readonly GroupSelectionChange _groupSelectionChanged;

        public GroupIndexSelector(
            Options.VisualizerOptions visualizerOptions,
            CalculateGroupCount getGroupCount,
            GroupSelectionChange groupSelectionChanged)
        {
            _visualizerOptions = visualizerOptions;
            _getGroupCount = getGroupCount;
            _groupSelectionChanged = groupSelectionChanged;

            _visualizerOptions.PropertyChanged += (sender, args) =>
            {
                if (args.PropertyName == nameof(Options.VisualizerOptions.NDRange3D))
                    X = 0; // reset group index to clear errors
            };
        }

        private uint LimitIndex(uint index, uint limit) =>
            (index < limit || !_visualizerOptions.NDRange3D) ? index : limit - 1;

        public void OnDataAvailable()
        {
            _dataAvailable = true;
            Update();
            RaiseGroupSelectionChanged();
        }

        public void Update()
        {
            var index = _visualizerOptions.NDRange3D ? (X + Y * DimX + Z * DimX * DimY) : X;
            var coordinates = _visualizerOptions.NDRange3D ? $"({X}; {Y}; {Z})" : $"({X})";
            var args = new GroupIndexChangedEventArgs(coordinates, index, GroupSize);
            IndexChanged?.Invoke(this, args);

            _error = args.IsValid ? null : $"Invalid group index: {index} >= {args.DataGroupCount}";

            ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(nameof(X)));
            ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(nameof(Y)));
            ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(nameof(Z)));
        }

        private void RaisePropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            if (propertyName == nameof(X) || propertyName == nameof(Y) || propertyName == nameof(Z) || propertyName == nameof(GroupSize))
                RaiseGroupSelectionChanged();
        }

        private void RaiseGroupSelectionChanged()
        {
            if (HasErrors || !_dataAvailable) return;
            string coordinates = _visualizerOptions.NDRange3D ? $"({X}; {Y}; {Z})" : $"({X})";
            _groupSelectionChanged(0, coordinates);
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

            Update();
            RaisePropertyChanged(propertyName);

            return true;
        }
    }
}
