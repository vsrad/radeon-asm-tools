using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using VSRAD.Package.Utils;

namespace VSRAD.Package.DebugVisualizer
{
    public sealed class ColumnHighlightRegion : DefaultNotifyPropertyChanged
    {
        private string _selector;
        public string Selector
        {
            get => _selector ?? "";
            set => SetField(ref _selector, value);
        }

        private ColumnHighlightColor _color;
        [JsonConverter(typeof(StringEnumConverter))]
        public ColumnHighlightColor Color
        {
            get => _color;
            set => SetField(ref _color, value);
        }
    }
}
