using System;
using System.Runtime.InteropServices;
using VSRAD.Package.Server;
using VSRAD.Package.Utils;

namespace VSRAD.Package.DebugVisualizer.SliceVisualizer
{
    public sealed class TypedSliceWatchView
    {
        public int RowCount => _view.RowCount;
        public int ColumnCount => _view.ColumnCount;
        public int RowHeader(int row) => _view.RowHeader(row);

        public bool IsSingleWordValue => _type == VariableType.Half;

        private readonly SliceWatchView _view;
        private readonly VariableType _type;
        private readonly TypedWatchValue _minValue;
        private readonly TypedWatchValue _maxValue;

        public TypedSliceWatchView(SliceWatchView view, VariableType type)
        {
            _view = view;
            _type = type;

            GetMinMaxValues(view, type, out _minValue, out _maxValue);
        }

        public string this[int row, int column]
        {
            get => DataFormatter.FormatDword(_type, _view[row, column]);
        }

        public bool IsInactiveCell(int row, int column) => _view.IsInactiveCell(row, column);

        public float GetRelativeValue(int row, int column, int word = 0)
        {
            switch (_type)
            {
                case VariableType.Uint:
                case VariableType.Hex:
                case VariableType.Bin:
                    return ((float)(_view[row, column] - _minValue.uintValue)) / (_maxValue.uintValue - _minValue.uintValue);
                case VariableType.Int:
                    return ((float)((int)_view[row, column] - _minValue.intValue)) / (_maxValue.intValue - _minValue.intValue);
                case VariableType.Float:
                    var floatValue = BitConverter.ToSingle(BitConverter.GetBytes(_view[row, column]), 0);
                    return (floatValue - _minValue.floatValue) / (_maxValue.floatValue - _minValue.floatValue);
                case VariableType.Half:
                    floatValue = Half.ToFloat(BitConverter.ToUInt16(BitConverter.GetBytes(_view[row, column]), startIndex: word * 2));
                    return (floatValue - _minValue.floatValue) / (_maxValue.floatValue - _minValue.floatValue);
            }
            throw new NotImplementedException();
        }

        private static void GetMinMaxValues(SliceWatchView view, VariableType type, out TypedWatchValue min, out TypedWatchValue max)
        {
            switch (type)
            {
                case VariableType.Uint:
                case VariableType.Hex:
                case VariableType.Bin:
                    uint umin =  uint.MaxValue;
                    uint umax = uint.MinValue;
                    for (int row = 0; row < view.RowCount; ++row)
                    {
                        for (int col = 0; col < view.ColumnCount; ++col)
                        {
                            uint value = view[row, col];
                            if (value < umin)
                                umin = value;
                            if (value > umax)
                                umax = value;
                        }
                    }
                    min = new TypedWatchValue { uintValue = umin };
                    max = new TypedWatchValue { uintValue = umax };
                    return;
                case VariableType.Int:
                    int imin = int.MaxValue;
                    int imax = int.MinValue;
                    for (int row = 0; row < view.RowCount; ++row)
                    {
                        for (int col = 0; col < view.ColumnCount; ++col)
                        {
                            int value = (int)view[row, col];
                            if (value < imin)
                                imin = value;
                            if (value > imax)
                                imax = value;
                        }
                    }
                    min = new TypedWatchValue { intValue = imin };
                    max = new TypedWatchValue { intValue = imax };
                    return;
                case VariableType.Float:
                    float fmin = float.MaxValue;
                    float fmax = float.MinValue;
                    for (int row = 0; row < view.RowCount; ++row)
                    {
                        for (int col = 0; col < view.ColumnCount; ++col)
                        {
                            float value = BitConverter.ToSingle(BitConverter.GetBytes(view[row, col]), 0);
                            if (float.IsNaN(value))
                                continue;
                            if (value < fmin)
                                fmin = value;
                            if (value > fmax)
                                fmax = value;
                        }
                    }
                    min = new TypedWatchValue { floatValue = fmin };
                    max = new TypedWatchValue { floatValue = fmax };
                    return;
                case VariableType.Half:
                    fmin = float.MaxValue;
                    fmax = float.MinValue;
                    for (int row = 0; row < view.RowCount; ++row)
                    {
                        for (int col = 0; col < view.ColumnCount; ++col)
                        {
                            byte[] bytes = BitConverter.GetBytes(view[row, col]);
                            float firstHalf = Half.ToFloat(BitConverter.ToUInt16(bytes, 0));
                            float secondHalf = Half.ToFloat(BitConverter.ToUInt16(bytes, 2));
                            if (!float.IsNaN(firstHalf))
                            {
                                if (firstHalf < fmin)
                                    fmin = firstHalf;
                                if (firstHalf > fmax)
                                    fmax = firstHalf;
                            }
                            if (!float.IsNaN(secondHalf))
                            {
                                if (secondHalf < fmin)
                                    fmin = secondHalf;
                                if (secondHalf > fmax)
                                    fmax = secondHalf;
                            }
                        }
                    }
                    min = new TypedWatchValue { floatValue = fmin };
                    max = new TypedWatchValue { floatValue = fmax };
                    return;
            }
            throw new NotImplementedException();
        }

        [StructLayout(LayoutKind.Explicit)]
#pragma warning disable CA1815 // Override equals and operator equals on value types (not used)
        private struct TypedWatchValue
#pragma warning restore CA1815 // Override equals and operator equals on value types (not used)
        {
            [FieldOffset(0)]
            public uint uintValue;         // VariableType.Uint, VariableType.Hex, VariableType.Bin
            [FieldOffset(0)]
            public int intValue;           // VariableType.Int
            [FieldOffset(0)]
            public float floatValue;       // VariableType.Float, VariableType.Half
        };
    }
}
