using System.Windows.Controls;
using System.Windows.Input;

namespace VSRAD.Syntax.FunctionList
{
    public partial class FunctionListControl
    {
        private readonly FunctionListContext _context;

        public FunctionListControl()
        {
            _context = new FunctionListContext();
            DataContext = _context;
            InitializeComponent();
        }

        private void FunctionListContentGrid_KeyDown(object sender, KeyEventArgs e) => Keyboard.Focus(Search);

        private void FunctionsName_MouseDoubleClick(object sender, MouseButtonEventArgs e) => NavigateToCurrentItem();

        private void Functions_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                NavigateToCurrentItem();
            }
        }

        private void FunctionListWindow_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
        }

        private void Search_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Up || e.Key == Key.Down)
                Keyboard.Focus(ListView);
        }

        private void ListView_ScrollChanged(object sender, ScrollChangedEventArgs e) =>
            AutosizeColumnsWidth();

        private void ListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ListView.SelectedItem != null)
                ListView.ScrollIntoView(ListView.SelectedItem);
        }

        private void NavigateToCurrentItem() =>
            _context.NavigateToCurrentItemCommand.Execute();

        private void AutosizeColumnsWidth()
        {
            // This is a well know behaviour of GridView
            // https://stackoverflow.com/questions/560581/how-to-autosize-and-right-align-gridviewcolumn-data-in-wpf/1931423#1931423
            GridView.Columns[0].Width = 0;
            GridView.Columns[0].Width = double.NaN;
            GridView.Columns[1].Width = 0;
            GridView.Columns[1].Width = double.NaN;
        }
    }
}