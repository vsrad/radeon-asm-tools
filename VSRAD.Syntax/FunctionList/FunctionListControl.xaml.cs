using VSRAD.Syntax.FunctionList.Commands;
using static VSRAD.Syntax.Options.GeneralOptionPage;
using Microsoft.VisualStudio.Shell;
using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Task = System.Threading.Tasks.Task;

namespace VSRAD.Syntax.FunctionList
{
    public partial class FunctionListControl : UserControl
    {
        public SortState SortState
        {
            get { return _sortState; }
            set
            {
                if (_sortState != value)
                {
                    _sortState = value;
                    SortAndReloadFunctionList();
                }
            }
        }
        public bool Autoscroll { get; set; }

        private readonly OleMenuCommandService commandService;
        private SortState _sortState;
        private bool _isHideLineNumber;
        private List<FunctionListItem> _tokens;
        private string _searchText;

        public FunctionListControl(OleMenuCommandService service)
        {
            var showHideLineNumberCommand = new CommandID(FunctionListCommand.CommandSet, Constants.ShowHideLineNumberCommandId);
            service.AddCommand(new MenuCommand(ShowHideLineNumber, showHideLineNumberCommand));
            _tokens = new List<FunctionListItem>();
            _isHideLineNumber = false;
            _searchText = string.Empty;

            InitializeComponent();
            commandService = service;
            tokens.LayoutUpdated += (s, e) => SetLineNumberColumnWidth();
        }

        public async Task UpdateListAsync(IEnumerable<FunctionListItem> newTokens)
        {
            _tokens = newTokens.ToList();
            var filteredTokens = Helper.SortAndFilter(_tokens, SortState, _searchText);

            await AddTokensToViewAsync(filteredTokens);
        }

        public void ClearList()
        {
            _tokens = new List<FunctionListItem>();
            tokens.Items.Clear();
        }

        private void SortAndReloadFunctionList()
        {
            var filteredTokens = Helper.SortAndFilter(_tokens, SortState, _searchText);
            ReloadFunctionList(filteredTokens);
        }

        private void ReloadFunctionList(IEnumerable<FunctionListItem> tokens) =>
            ThreadHelper.JoinableTaskFactory.RunAsync(() => AddTokensToViewAsync(tokens));

        private async Task AddTokensToViewAsync(IEnumerable<FunctionListItem> functionListTokens)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            tokens.Items.Clear();
            foreach (var token in functionListTokens)
                tokens.Items.Add(token);

            /* Needs to update line number column width after adding new items */
            AutosizeColumns();
        }

        private void ByNumber_Click(object sender, RoutedEventArgs e)
        {
            SortState = SortState != SortState.ByLine
                ? SortState.ByLine
                : SortState.ByLineDescending;

            SortAndReloadFunctionList();
        }

        private void ByName_Click(object sender, RoutedEventArgs e)
        {
            SortState = SortState != SortState.ByName
                ? SortState.ByName
                : SortState.ByNameDescending;

            SortAndReloadFunctionList();
        }

        private void FunctionsName_MouseDoubleClick(object sender, MouseButtonEventArgs e) => GoToSelectedItem();

        private void FunctionListWindow_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (null != commandService)
            {
                CommandID menuID = new CommandID(
                    FunctionListCommand.CommandSet,
                    Constants.FunctionListMenu);
                Point p = PointToScreen(e.GetPosition(this));
                commandService.ShowContextMenu(menuID, (int)p.X, (int)p.Y);
            }
        }

        private void AutosizeColumns()
        {
            /* This is a well know behaviour of GridView https://stackoverflow.com/questions/560581/how-to-autosize-and-right-align-gridviewcolumn-data-in-wpf/1931423#1931423 */
            functionsGridView.Columns[0].Width = 0;
            if (!_isHideLineNumber)
                functionsGridView.Columns[0].Width = double.NaN;
            functionsGridView.Columns[1].Width = 0;
            functionsGridView.Columns[1].Width = double.NaN;
        }

        private void SetLineNumberColumnWidth()
        {
            // Line Number ActualWidth will apply only after UpdateLayout only then it can be compared with min width
            if (functionsGridView.Columns[0].ActualWidth > 0)
                functionsGridView.Columns[0].Width = Math.Max(functionsGridView.Columns[0].ActualWidth, 45.4 /*min width equals to 5 digits*/);

            LineNumberButtonColumn.Width = new GridLength(functionsGridView.Columns[0].ActualWidth);
        }

        private void ShowHideLineNumber(object sender, EventArgs e)
        {
            _isHideLineNumber = !_isHideLineNumber;
            AutosizeColumns();
        }

        private void Search_TextChanged(object sender, TextChangedEventArgs e)
        {
            _searchText = Search.Text;
            var filteredTokens = Helper.Filter(_tokens, _searchText);
            ReloadFunctionList(filteredTokens);
        }

        public void OnClearSearchField() => Search.Text = "";

        public void GoToSelectedItem()
        {
            var token = (FunctionListItem)tokens.SelectedItem;
            if (token != null)
                token.Navigate();
        }

        private void FunctionListContentGridOnLoad(object sender, RoutedEventArgs e) => functionListContentGrid.Focus();

        private void FunctionListContentGrid_KeyDown(object sender, KeyEventArgs e) => Keyboard.Focus(Search);

        private void Search_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Up || e.Key == Key.Down)
                Keyboard.Focus(tokens);
        }

        private void Functions_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                GoToSelectedItem();
            }
        }
    }
}