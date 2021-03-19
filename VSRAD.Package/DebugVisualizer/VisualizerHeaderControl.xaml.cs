using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using VSRAD.Package.Options;
using VSRAD.Package.Utils;

namespace VSRAD.Package.DebugVisualizer
{
    public sealed partial class VisualizerHeaderControl : UserControl
    {
        public static readonly DependencyProperty OptionsProperty =
            DependencyProperty.Register(nameof(DebugOptions), typeof(DebuggerOptions), typeof(VisualizerHeaderControl), new PropertyMetadata(null));

        public DebuggerOptions DebugOptions
        {
            get => (DebuggerOptions)GetValue(OptionsProperty); set => SetValue(OptionsProperty, value); 
        }

        public ICommand PinnedButtonCommand { get; }
        public VisualizerHeaderControl()
        {
            InitializeComponent();
            PinnedButtonCommand = new WpfDelegateCommand(PinnedButtonClick);
        }

        private void PinnedButtonClick(object param)
        {
            if (!(param is PinnableElement<string> element)) return;
            DebugOptions.LastAppArgs.TogglePinnedState(element);
        }

        private void DropdownStateChanged(object sender, RoutedEventArgs e)
        {
            DebugOptions.LastAppArgs.UpdateElementsOrder();
        }
    }
}
