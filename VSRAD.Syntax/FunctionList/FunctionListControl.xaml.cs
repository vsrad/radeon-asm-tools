using VSRAD.Syntax.FunctionList.Commands;
using static VSRAD.Syntax.Options.GeneralOptionPage;
using Microsoft.VisualStudio.Shell;
using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Task = System.Threading.Tasks.Task;
using System.Threading;
using System.Linq;
using VSRAD.Syntax.Options;

namespace VSRAD.Syntax.FunctionList
{
    public partial class FunctionListControl : UserControl
    {

        private SortState SortState;
        private bool Autoscroll;
        private TypeFilterState TypeFilterState = TypeFilterState.FL;

        private readonly OleMenuCommandService _commandService;
        private bool hideLineNumber;
        private List<FunctionListItem> items;
        private string searchText;
        private FunctionListItem lastHighlightedItem;

        public FunctionListControl(OptionsProvider optionsProvider, OleMenuCommandService service)
        {
            items = new List<FunctionListItem>();
            hideLineNumber = false;
            searchText = string.Empty;
            SortState = optionsProvider.SortOptions;
            Autoscroll = optionsProvider.Autoscroll;
            _commandService = service;

            InitializeComponent();
            tokens.LayoutUpdated += (s, e) => SetLineNumberColumnWidth();
            optionsProvider.OptionsUpdated += OptionsUpdated;
            typeFilter.Content = TypeFilterState.ToString();
        }

        private void OptionsUpdated(OptionsProvider sender)
        {
            if (sender.SortOptions != SortState)
            {
                SortState = sender.SortOptions;
                SortAndReloadFunctionList();
            }
            Autoscroll = sender.Autoscroll;
        }

        #region public methods
        public void ShowHideLineNumber()
        {
            hideLineNumber = !hideLineNumber;
            AutosizeColumns();
        }

        public void ClearSearch() => Search.Text = "";

        public void GoToSelectedItem()
        {
            var token = (FunctionListItem)tokens.SelectedItem;
            if (token != null)
                token.Navigate();
        }

        public async Task UpdateListAsync(List<FunctionListItem> newTokens, CancellationToken cancellationToken)
        {
            items = newTokens;
            var filteredTokens = Helper.FilterAndSort(items, SortState, TypeFilterState, searchText);
            if (cancellationToken.IsCancellationRequested) return;

            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
            
            AddTokensToView(filteredTokens);
            if (lastHighlightedItem != null)
                HighlightItemAtLine(lastHighlightedItem.LineNumber);
        }

        public void ClearList()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            items = new List<FunctionListItem>();
            tokens.Items.Clear();

            ClearHighlightItem();
        }

        public void HighlightItemAtLine(int lineNumber)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var item = items.FirstOrDefault(i => i.LineNumber == lineNumber);

            if (lastHighlightedItem != null) lastHighlightedItem.IsCurrentWorkingItem = false;
            if (item == null) return;

            item.IsCurrentWorkingItem = true;
            lastHighlightedItem = item;

            if (Autoscroll) tokens.ScrollIntoView(item);
        }

        public void ClearHighlightItem()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (lastHighlightedItem != null)
            {
                lastHighlightedItem.IsCurrentWorkingItem = false;
                lastHighlightedItem = null;
            }
        }
        #endregion

        private void SortAndReloadFunctionList()
        {
            var filteredTokens = Helper.FilterAndSort(items, SortState, TypeFilterState, searchText);
            ReloadFunctionList(filteredTokens);
        }

        private void ReloadFunctionList(IEnumerable<FunctionListItem> tokens) => AddTokensToView(tokens);

        private void AddTokensToView(IEnumerable<FunctionListItem> functionListTokens)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            tokens.Items.Clear();
            foreach (var token in functionListTokens)
                tokens.Items.Add(token);

            /* Needs to update line number column width after adding new items */
            AutosizeColumns();
        }

        private void AutosizeColumns()
        {
            /* This is a well know behaviour of GridView https://stackoverflow.com/questions/560581/how-to-autosize-and-right-align-gridviewcolumn-data-in-wpf/1931423#1931423 */
            functionsGridView.Columns[0].Width = 0;
            if (!hideLineNumber)
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

        #region control handlers
        private void FunctionListContentGridOnLoad(object sender, RoutedEventArgs e) => functionListContentGrid.Focus();

        private void FunctionListContentGrid_KeyDown(object sender, KeyEventArgs e) => Keyboard.Focus(Search);

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
            if (null != _commandService)
            {
                CommandID menuID = new CommandID(AbstractFunctionListCommand.CommandSet, Constants.FunctionListMenu);
                Point p = PointToScreen(e.GetPosition(this));
                _commandService.ShowContextMenu(menuID, (int)p.X, (int)p.Y);
            }
        }

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

        private void Search_TextChanged(object sender, TextChangedEventArgs e)
        {
            searchText = Search.Text;
            var filteredTokens = Helper.FilterText(items, searchText);
            ReloadFunctionList(filteredTokens);
        }

        private void TypeFilter_Click(object sender, RoutedEventArgs e)
        {
            switch (TypeFilterState)
            {
                case TypeFilterState.FL: TypeFilterState = TypeFilterState.F; break;
                case TypeFilterState.F: TypeFilterState = TypeFilterState.L; break;
                case TypeFilterState.L: TypeFilterState = TypeFilterState.FL; break;
            }

            typeFilter.Content = TypeFilterState.ToString();
            SortAndReloadFunctionList();
        }
        #endregion
    }

    public enum TypeFilterState
    {
        FL = 1, // functions and labels
        F = 2, // only functions
        L = 3, // only labels
    }
}