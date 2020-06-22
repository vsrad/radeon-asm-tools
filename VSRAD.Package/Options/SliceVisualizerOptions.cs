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

        private int _subgroupSize = 64;
        public int SubgroupSize { get => _subgroupSize; set => SetField(ref _subgroupSize, value); }

        private bool _useHeatMap = false;
        public bool UseHeatMap { get => _useHeatMap; set => SetField(ref _useHeatMap, value); }

        private bool _transposedView = false;
        public bool TransposedView { get => _transposedView; set => SetField(ref _transposedView, value); }

        private int _groupsInRow = 1;
        public int GroupsInRow { get => _groupsInRow; set => SetField(ref _groupsInRow, value); }
    }
}
