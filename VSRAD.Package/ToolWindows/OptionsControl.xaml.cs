using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using VSRAD.Package.Options;
using VSRAD.Package.ProjectSystem;
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

            public string ConnectionInfo => _channel.ConnectionOptions.ToString();

            public string DisconnectLabel
            {
                get => _channel.ConnectionState == ClientState.Connected ? "Disconnect"
                     : _channel.ConnectionState == ClientState.Connecting ? "Connecting..." : "Disconnected";
            }

            public ICommand DisconnectCommand { get; }

            private readonly ICommunicationChannel _channel;

            public Context(ProjectOptions options, ICommunicationChannel channel)
            {
                Options = options;
                Options.Profiles.PropertyChanged += (s, e) => { if (e.PropertyName == nameof(Options.Profiles.Keys)) RaisePropertyChanged(nameof(ProfileNames)); };
                _channel = channel;
                _channel.ConnectionStateChanged += ConnectionStateChanged;
                DisconnectCommand = new WpfDelegateCommand((_) => _channel.ForceDisconnect(), isEnabled: _channel.ConnectionState == ClientState.Connected);
            }

            private void ConnectionStateChanged()
            {
                RaisePropertyChanged(nameof(ConnectionInfo));
                RaisePropertyChanged(nameof(DisconnectLabel));
                ((WpfDelegateCommand)DisconnectCommand).IsEnabled = _channel.ConnectionState == ClientState.Connected;
            }
        }

        private readonly ProjectOptions _projectOptions;
        private readonly MacroEditManager _macroEditor;

        public OptionsControl(IToolWindowIntegration integration)
        {
            _projectOptions = integration.ProjectOptions;
            _macroEditor = integration.MacroEditor;
            DataContext = new Context(integration.ProjectOptions, integration.CommunicationChannel);
            InitializeComponent();
        }

        // TODO: can freeze here
        private void EditProfiles(object sender, RoutedEventArgs e)
        {
            new ProjectSystem.Profiles.ProfileOptionsWindow(_macroEditor, _projectOptions).ShowDialog();
        }

        private void AlignmentButtonClick(object sender, RoutedEventArgs e)
        {
            var button = ((Button)sender);
            DebugVisualizer.ContentAlignment alignment;
            switch (button.Content)
            {
                case "C":
                    alignment = DebugVisualizer.ContentAlignment.Center;
                    break;
                case "R":
                    alignment = DebugVisualizer.ContentAlignment.Right;
                    break;
                default:
                    alignment = DebugVisualizer.ContentAlignment.Left;
                    break;
            }
            switch (button.Tag)
            {
                case "data":
                    _projectOptions.VisualizerAppearance.DataColumnAlignment = alignment;
                    break;
                case "headers":
                    _projectOptions.VisualizerAppearance.HeadersAlignment = alignment;
                    break;
                default:
                    _projectOptions.VisualizerAppearance.NameColumnAlignment = alignment;
                    break;
            }
        }
    }

    public sealed class BreakModeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            switch (value)
            {
                case Deborgar.BreakMode.SingleRoundRobin:
                    return "Single active breakpoint, round-robin";
                case Deborgar.BreakMode.SingleRerun:
                    return "Single active breakpoint, rerun same line";
                case Deborgar.BreakMode.Multiple:
                    return "Multiple active breakpoints";
                default:
                    return DependencyProperty.UnsetValue;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) => value;
    }

    public sealed class ScalingModeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            switch (value)
            {
                case DebugVisualizer.ScalingMode.ResizeColumnAllowWide:
                    return "Resize Column, allow wide 1st column";
                case DebugVisualizer.ScalingMode.ResizeColumn:
                    return "Resize Column";
                case DebugVisualizer.ScalingMode.ResizeTable:
                    return "Resize Table";
                default:
                    return DependencyProperty.UnsetValue;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) => value;
    }
}
