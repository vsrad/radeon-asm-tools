using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.ComponentModel;
using System.Globalization;
using VSRAD.Package.Utils;

namespace VSRAD.Package.Options
{
    public sealed class VisualizerOptions : DefaultNotifyPropertyChanged
    {
        private bool _showSystemVariable = true;
        public bool ShowSystemVariable { get => _showSystemVariable; set => SetField(ref _showSystemVariable, value); }

        private bool _maskLanes = true;
        public bool MaskLanes { get => _maskLanes; set => SetField(ref _maskLanes, value); }

        private bool _ndrange3d = false;
        public bool NDRange3D { get => _ndrange3d; set => SetField(ref _ndrange3d, value); }

        private uint _wavemapElementSize = 8;
        [DefaultValue(8)]
        public uint WavemapElementSize { get => _wavemapElementSize; set => SetField(ref _wavemapElementSize, Math.Max(value, 7)); }

        private bool _showColumnsField;
        [DefaultValue(true)]
        public bool ShowColumnsField { get => _showColumnsField; set => SetField(ref _showColumnsField, value); }

        private bool _showAppArgsField;
        [DefaultValue(true)]
        public bool ShowAppArgsField { get => _showAppArgsField; set => SetField(ref _showAppArgsField, value); }

        private bool _showBreakArgsField;
        [DefaultValue(true)]
        public bool ShowBreakArgsField { get => _showBreakArgsField; set => SetField(ref _showBreakArgsField, value); }

        private bool _showWavemap;
        [DefaultValue(true)]
        public bool ShowWavemap { get => _showWavemap; set => SetField(ref _showWavemap, value); }

        private bool _matchBracketsOnAddToWatches;
        [DefaultValue(false)]
        public bool MatchBracketsOnAddToWatches { get => _matchBracketsOnAddToWatches; set => SetField(ref _matchBracketsOnAddToWatches, value); }
    }
}
