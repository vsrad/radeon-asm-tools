using Microsoft.VisualStudio.PlatformUI;
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

namespace VSRAD.Package.Commands
{
    /// <summary>
    /// Interaction logic for AddToWatchesCustomSliceEditor.xaml
    /// </summary>
    public partial class AddToWatchesCustomSliceEditor : DialogWindow
    {
        public delegate void CreateSlice(uint start, uint step, uint count, string watchName);
        private CreateSlice _createSlice;

        private string _watchName;

        public AddToWatchesCustomSliceEditor(CreateSlice createSliceHandler, string watchName)
        {
            _watchName = watchName;
            _createSlice = createSliceHandler;
            InitializeComponent();
        }

        private void HandleOK(object sender, RoutedEventArgs e)
        {
            _createSlice(StartControl.Value, StepControl.Value, CountControl.Value, _watchName);
            Close();
        }
    }
}
