using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Input;
using VSRAD.Package.Options;
using VSRAD.Package.ProjectSystem.Macros;
using VSRAD.Package.Server;
using VSRAD.Package.Utils;

namespace VSRAD.Package.ToolWindows
{
    public partial class OptionsControl : UserControl
    {
        public sealed class Context : DefaultNotifyPropertyChanged
        {
            public ProjectOptions Options { get; }
            public IReadOnlyList<string> ProfileNames => Options.Profiles.Keys.ToList();

            private string _disconnectLabel = "Disconnected";
            public string DisconnectLabel { get => _disconnectLabel; set => SetField(ref _disconnectLabel, value); }

            private string _connectionInfo = "";
            public string ConnectionInfo { get => _connectionInfo; set => SetField(ref _connectionInfo, value); }

            public ICommand DisconnectCommand { get; }

            private readonly ICommunicationChannelManager _channelManager;

            public Context(ProjectOptions options, ICommunicationChannelManager channel)
            {
                Options = options;
                Options.Profiles.PropertyChanged += (s, e) => { if (e.PropertyName == nameof(Options.Profiles.Keys)) RaisePropertyChanged(nameof(ProfileNames)); };
                _channelManager = channel;
                _channelManager.ConnectionStateChanged += ConnectionStateChanged;
                DisconnectCommand = new WpfDelegateCommand((_) => _channelManager.ForceDisconnect(), isEnabled: false);
                ConnectionStateChanged();
            }

            private void ConnectionStateChanged()
            {
                DisconnectLabel = _channelManager.ChannelState.state == ClientState.Connected ? "Disconnect"
                                : _channelManager.ChannelState.state == ClientState.Connecting ? "Connecting..." : "Disconnected";
                ConnectionInfo = _channelManager.ChannelState.connectionInfo;
                ((WpfDelegateCommand)DisconnectCommand).IsEnabled = _channelManager.ChannelState.state == ClientState.Connected;
            }
        }

        private readonly ProjectOptions _projectOptions;
        private readonly MacroEditor _macroEditor;

        public OptionsControl(IToolWindowIntegration integration)
        {
            _projectOptions = integration.ProjectOptions;
            _macroEditor = integration.GetExport<MacroEditor>();
            DataContext = new Context(integration.ProjectOptions, integration.GetExport<ICommunicationChannelManager>());
            InitializeComponent();
            ColoringRegionsGrid.PreviewMouseWheel += (s, e) =>
            {
                if (ColoringRegionsGrid.IsMouseOver)
                    ControlScrollViewer.ScrollToVerticalOffset(ControlScrollViewer.VerticalOffset - e.Delta * 0.125);
            };
        }

        // TODO: can freeze here
        private void EditProfiles(object sender, System.Windows.RoutedEventArgs e)
        {
            new ProjectSystem.Profiles.ProfileOptionsWindow(_macroEditor, _projectOptions).ShowDialog();
        }
    }
}
