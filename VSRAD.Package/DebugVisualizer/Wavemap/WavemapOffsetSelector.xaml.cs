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
        public static readonly DependencyProperty XValueProperty =
            DependencyProperty.Register(nameof(XValue), typeof(int), typeof(WavemapOffsetSelector),
                new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, DependencyPropertyChanged));

        public static readonly DependencyProperty YValueProperty =
            DependencyProperty.Register(nameof(YValue), typeof(int), typeof(WavemapOffsetSelector),
                new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, DependencyPropertyChanged));

        public static readonly DependencyProperty LabelProperty =
            DependencyProperty.Register(nameof(Label), typeof(string), typeof(WavemapOffsetSelector),
                new FrameworkPropertyMetadata("0 - 99 / 0 - 7", FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, DependencyPropertyChanged));

        public event PropertyChangedEventHandler PropertyChanged;
        public string Label
        {
            get => (string)GetValue(LabelProperty);
            set => SetValue(LabelProperty, value);
        }

        public int XValue
        {
            get => (int)GetValue(XValueProperty);
            set
            {
                var newValue = value;
                if (newValue < 0)
                    newValue = 0;
                SetValue(XValueProperty, newValue);

                Label = $"{100 * XValue} - {100 * XValue + 99} / {8 * YValue} - {8 * YValue + 7}";
            }
        }

        public int YValue
        {
            get => (int)GetValue(YValueProperty);
            set
            {
                var newValue = value;
                if (newValue < 0)
                    newValue = 0;
                SetValue(YValueProperty, newValue);

                Label = $"{100 * XValue} - {100 * XValue + 99} / {8 * YValue} - {8 * YValue + 7}";
            }
        }

        public WavemapOffsetSelector()
        {
            InitializeComponent();
            // Do not change this.DataContext, it inherits from the control that uses NumberInput
            Root.DataContext = this;
        }

        private void XIncrement(object sender, RoutedEventArgs e) => XValue++;
        private void XDecrement(object sender, RoutedEventArgs e) => XValue--;

        private void YIncrement(object sender, RoutedEventArgs e) => YValue++;
        private void YDecrement(object sender, RoutedEventArgs e) => YValue--;

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
