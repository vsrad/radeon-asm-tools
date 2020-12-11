using System;
using System.Collections.Generic;
using System.ComponentModel;
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

namespace VSRAD.Package.DebugVisualizer.Wavemap
{
    /// <summary>
    /// Interaction logic for WavemapOffsetSelector.xaml
    /// </summary>
    public sealed partial class WavemapOffsetSelector : UserControl, INotifyPropertyChanged
    {
        public static readonly DependencyProperty ValueProperty =
            DependencyProperty.Register(nameof(Value), typeof(uint), typeof(WavemapOffsetSelector),
                new FrameworkPropertyMetadata((uint)0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, DependencyPropertyChanged));

        public event PropertyChangedEventHandler PropertyChanged;

        public uint Value
        {
            get => (uint)GetValue(ValueProperty);
            set
            {
                var newValue = value;
                if (newValue < 0)
                    newValue = 0;

                SetValue(ValueProperty, newValue);
            }
        }

        public WavemapOffsetSelector()
        {
            InitializeComponent();
            // Do not change this.DataContext, it inherits from the control that uses NumberInput
            Root.DataContext = this;
        }

        private void XIncrement(object sender, RoutedEventArgs e) => Value++;

        private void XDecrement(object sender, RoutedEventArgs e) => Value--;

        private static void DependencyPropertyChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
        {
            //var numberInput = (NumberInput)dependencyObject;
            //switch (args.Property.Name)
            //{
            //    case nameof(Value): numberInput.Value = (uint)args.NewValue; break;
            //    case nameof(Step): numberInput.Step = (uint)args.NewValue; break;
            //    case nameof(Minimum): numberInput.Minimum = (uint)args.NewValue; break;
            //    case nameof(Maximum): numberInput.Maximum = (uint)args.NewValue; break;
            //}
        }
    }
}
