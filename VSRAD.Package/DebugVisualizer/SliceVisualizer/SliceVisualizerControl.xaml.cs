using Microsoft.VisualStudio.Debugger.Interop;
using System.Collections.Generic;
using System.Windows.Controls;
using VSRAD.Package.ProjectSystem;
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
        private readonly IToolWindowIntegration _integration;

        public SliceVisualizerControl(IToolWindowIntegration integration)
        {
            _integration = integration;
            var tableFontAndColor = new FontAndColorProvider();
            InitializeComponent();
            integration.BreakEntered += BreakEntered;
            _table = new SliceVisualizerTable(tableFontAndColor);
            headerControl.Setup(integration, WatchSelected, ToggleHeatMap);
            tableHost.Setup(_table);
        }

        public void BreakEntered(Server.BreakState breakState)
        {
            Ensure.ArgumentNotNull(breakState, nameof(breakState));
            _breakState = breakState;
            var selectedWatch = headerControl.GetSelectedWatch();
            if (selectedWatch != null)
                WatchSelected(selectedWatch);
        }

        private void WatchSelected(string watchName)
        {
            if (_breakState == null) return;
            var watch = _breakState.Data.GetSliceWatch(watchName, headerControl.GroupsInRow());
            _table.DisplayWatch(watch);
        }

        private void ToggleHeatMap(bool heatMapActive)
        {
            if (heatMapActive)
                _table.ApplyDataStyling(_integration.ProjectOptions);
        }
    }
}
