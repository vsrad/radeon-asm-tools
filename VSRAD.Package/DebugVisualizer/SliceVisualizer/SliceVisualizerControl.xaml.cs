using System.Collections.Generic;
using System.Windows.Controls;
using VSRAD.Package.ToolWindows;
using VSRAD.Package.Utils;

namespace VSRAD.Package.DebugVisualizer.SliceVisualizer
{
    /// <summary>
    /// Interaction logic for SliceVisualizerControl.xaml
    /// </summary>
    public partial class SliceVisualizerControl : UserControl
    {
        private Server.BreakState _breakState;
        private SliceVisualizerTable _table;

        public SliceVisualizerControl(IToolWindowIntegration integration)
        {
            InitializeComponent();
            integration.BreakEntered += BreakEntered;
            _table = new SliceVisualizerTable();
            headerControl.Setup(integration, WatchSelected);
            tableHost.Setup(_table);
        }

        public void BreakEntered(Server.BreakState breakState)
        {
            Ensure.ArgumentNotNull(breakState, nameof(breakState));
            _breakState = breakState;
            /*  rewrite BreakState
            var selectedWatch = headerControl.GetSelectedWatch();
            if (selectedWatch != null)
                WatchSelected(selectedWatch);
            */
        }

        private void WatchSelected(string watchName)
        {
            if (_breakState == null) return;
            // TODO: fetch all groups
            var data = new List<uint[]>();
            var groupData = new uint[_breakState.GroupSize];
            _breakState.TryGetWatch(watchName, out groupData);
            data.Add(groupData);
            _table.DisplayWatch(data);
        }
    }
}
