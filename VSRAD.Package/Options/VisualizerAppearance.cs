using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VSRAD.Package.DebugVisualizer;
using VSRAD.Package.Utils;

namespace VSRAD.Package.Options
{
    public sealed class VisualizerAppearance : DefaultNotifyPropertyChanged
    {
        private ContentAlignment _nameColumnAlignment = ContentAlignment.Left;
        public ContentAlignment NameColumnAlignment
        {
            get => _nameColumnAlignment;
            set => SetField(ref _nameColumnAlignment, value);
        }

        private ContentAlignment _dataColumnAlignment = ContentAlignment.Left;
        public ContentAlignment DataColumnAlignment
        {
            get => _dataColumnAlignment;
            set => SetField(ref _dataColumnAlignment, value);
        }
    }
}
