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
    public sealed partial class WavemapOffsetSelector : UserControl
    {
        public static readonly DependencyProperty WaveInfoProperty =
            DependencyProperty.Register(nameof(WaveInfo), typeof(string), typeof(WavemapOffsetSelector),
                new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        public string WaveInfo
        {
            get => (string)GetValue(WaveInfoProperty);
            set => SetValue(WaveInfoProperty, value);
        }
       
        public void Setup(VisualizerContext context, WavemapImage image)
        {
            XOffsetSelector.Setup(context, image);
        }
        
        public WavemapOffsetSelector()
        {
            InitializeComponent();
            // Do not change this.DataContext, it inherits from the control that uses NumberInput
            Root.DataContext = this;
        }
    }
}
