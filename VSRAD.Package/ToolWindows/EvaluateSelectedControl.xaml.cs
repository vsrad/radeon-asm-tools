using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using VSRAD.Package.ProjectSystem;

namespace VSRAD.Package.ToolWindows
{
    public partial class EvaluateSelectedControl : UserControl
    {

        private readonly IToolWindowIntegration _integration;

        public EvaluateSelectedControl(IToolWindowIntegration integration)
        {
            _integration = integration;
            InitializeComponent();
        }

        public void UpdateWatches(string watchName, string[] values)
        {
            Values.ColumnDefinitions.Clear();
            Values.Children.Clear();

            this.WatchName.Text = watchName;
            for (var i = 0; i < values.Length; i++)
            {
                var watchBlock = CreateWatchTextBlock(values[i]);
                var border = CreateWatchBorder();
                border.Child = watchBlock;

                Values.ColumnDefinitions.Add(new ColumnDefinition());
                Grid.SetRow(border, 0);
                Grid.SetColumn(border, i);
                Values.Children.Add(border);
            }
            this.UpdateLayout();
        }

        private static Border CreateWatchBorder()
        {
            var border = new Border
            {
                BorderBrush = Brushes.LightGray,
                BorderThickness = new Thickness(1),
                Height = double.NaN,
            };
            return border;
        }

        private static TextBlock CreateWatchTextBlock(string formattedValue)
        {
            var watchValue = new TextBlock
            {
                Text = formattedValue,
                FontSize = 10,
                Margin = new Thickness(10, 3, 10, 3),
                Height = double.NaN,
            };
            return watchValue;
        }
    }
}
