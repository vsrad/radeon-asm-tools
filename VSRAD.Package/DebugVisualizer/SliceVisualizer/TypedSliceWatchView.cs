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

        public bool IsSingleWordValue => _type.Category == VariableCategory.Float && _type.Size == 16;

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
            get => DataFormatter.FormatDword(_type, _view[row, column], 2, 0, true); // TODO: remove hardcode
        }

        public bool IsInactiveCell(int row, int column) => _view.IsInactiveCell(row, column);

        public float GetRelativeValue(int row, int column, int word = 0)
        {
            throw new NotImplementedException("Current watch representation do not work with Slice Visualizer");
        }

        private static void GetMinMaxValues(SliceWatchView view, VariableType info, out TypedWatchValue min, out TypedWatchValue max)
        {
            throw new NotImplementedException("Current watch representation do not work with Slice Visualizer");
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
