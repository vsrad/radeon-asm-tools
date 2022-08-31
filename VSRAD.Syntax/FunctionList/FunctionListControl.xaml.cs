using VSRAD.Syntax.FunctionList.Commands;
using VSRAD.Syntax.Options;
using Microsoft.VisualStudio.Shell;
using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Linq;
using System.ComponentModel;

namespace VSRAD.Syntax.FunctionList
{
    public partial class FunctionListControl : UserControl
    {

        private SortState SortState;
        private bool Autoscroll;
        private TypeFilterState TypeFilterState = TypeFilterState.FL;

        private readonly OleMenuCommandService _commandService;
        private bool hideLineNumber;
        private IReadOnlyCollection<FunctionListItem> items;
        private string nameFilter;
        private FunctionListItem lastHighlightedItem;

        public FunctionListControl(OptionsProvider optionsProvider, OleMenuCommandService service)
        {
            items = new List<FunctionListItem>();
            hideLineNumber = false;
            nameFilter = string.Empty;
            _commandService = service;

            InitializeComponent();
            tokens.LayoutUpdated += (s, e) => SetLineNumberColumnWidth();
            typeFilter.Content = TypeFilterState.ToString();

            OptionsUpdated(optionsProvider);
            optionsProvider.OptionsUpdated += OptionsUpdated;
        }

        private void OptionsUpdated(OptionsProvider sender)
        {
            if (sender.SortOptions != SortState)
            {
                SortState = sender.SortOptions;
                ApplySort();
            }
            Autoscroll = sender.Autoscroll;
        }

        #region public methods
        public void ShowHideLineNumber()
        {
            hideLineNumber = !hideLineNumber;

            // Show/Hide first column
            functionsGridView.Columns[0].Width = hideLineNumber
                                               ? 0
                                               : double.NaN;
        }

        public void ClearSearch() => Search.Text = "";

        public void GoToSelectedItem()
        {
            var token = (FunctionListItem)tokens.SelectedItem;
            if (token != null)
                token.Navigate();
        }

        public void ReplaceListItems(IReadOnlyCollection<FunctionListItem> newTokens)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            items = newTokens;
            ApplyFilter();

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

        private void ApplyFilter()
        {
            var filteredTokens = Helper.Filter(items, TypeFilterState, nameFilter);
            AddTokensToView(filteredTokens);
        }

        private void ApplySort()
        {
            tokens.Items.SortDescriptions.Clear();

            var property = SortState == SortState.ByLine || SortState == SortState.ByLineDescending
                ? nameof(FunctionListItem.LineNumber)
                : nameof(FunctionListItem.Text);
            var direction = SortState == SortState.ByLine || SortState == SortState.ByName
                ? ListSortDirection.Ascending
                : ListSortDirection.Descending;

            var sortDescription = new SortDescription(property, direction);
            tokens.Items.SortDescriptions.Add(sortDescription);
        }

        private void AddTokensToView(IEnumerable<FunctionListItem> functionListTokens)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            tokens.Items.Clear();
            foreach (var token in functionListTokens)
                tokens.Items.Add(token);

            ApplySort();

            // Needs to update line number column width after adding new items
            AutosizeColumns();
        }

        private void AutosizeColumns()
        {
            // Setting to double.NaN frees ActualWidth upper bound
            functionsGridView.Columns[0].Width = hideLineNumber
                                               ? 0
                                               : double.NaN;
        }

        private void AdjustColumnsOnRendering()
        {
            // Line Number ActualWidth will apply only after UpdateLayout only then it can be compared with min width
            // Only happens when column is not hidden
            if (functionsGridView.Columns[0].ActualWidth > 0)
            {
                functionsGridView.Columns[0].Width = Math.Max(functionsGridView.Columns[0].ActualWidth, 45.4 /*min width equals to 5 digits*/);
            }
            functionsGridView.Columns[1].Width = FunctionListWindow.ActualWidth - functionsGridView.Columns[0].Width;
        }

        private void SetLineNumberColumnWidth()
        {
            AdjustColumnsOnRendering();
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

            ApplySort();
        }

        private void ByName_Click(object sender, RoutedEventArgs e)
        {
            SortState = SortState != SortState.ByName
                ? SortState.ByName
                : SortState.ByNameDescending;

            ApplySort();
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
            nameFilter = Search.Text;
            ApplyFilter();
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
            ApplyFilter();
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