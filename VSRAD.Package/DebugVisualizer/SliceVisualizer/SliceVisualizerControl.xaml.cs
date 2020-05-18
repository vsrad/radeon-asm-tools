using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using VSRAD.Package.Options;
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
        }

        private void WatchSelected(string watchName)
        {
            // TODO: fetch all groups, find a way to get current group size
            var data = new List<uint[]>();
            var groupData = new uint[64];
            _breakState.TryGetWatch(watchName, out groupData);
            data.Add(groupData);
            _table.DisplayWatch(data, 64);
        }
    }
}
