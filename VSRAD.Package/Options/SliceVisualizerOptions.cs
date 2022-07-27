using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VSRAD.Package.Utils;

namespace VSRAD.Package.Options
{
    public sealed class SliceVisualizerOptions : DefaultNotifyPropertyChanged
    {
        private string _visibleColumns = "0:1-63";
        public string VisibleColumns { get => _visibleColumns; set => SetField(ref _visibleColumns, value); }
    }
}
