using VSRAD.Syntax.Helpers;
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
using VSRAD.Syntax.Parser.Tokens;
using VSRAD.Syntax.Options;
using Microsoft.VisualStudio.Text;

namespace VSRAD.Syntax.FunctionList
{
    public partial class FunctionListControl : UserControl
    {
        private readonly OleMenuCommandService commandService;
        private SortState FunctionListSortState;
        private bool Autoscroll;
        private bool isHideLineNumber = false;
        private List<FunctionListItem> Tokens;
        private FunctionListItem LastHighlightedToken;
        private ITextSnapshot CurrentVersion;
        private string SearchText;

        public FunctionListControl(OleMenuCommandService service, OptionsProvider optionsProvider)
        {
            var showHideLineNumberCommand = new CommandID(FunctionListCommand.CommandSet, Constants.ShowHideLineNumberCommandId);
            service.AddCommand(new MenuCommand(ShowHideLineNumber, showHideLineNumberCommand));
            Tokens = new List<FunctionListItem>();
            LastHighlightedToken = new FunctionListItem(RadAsmTokenType.Unknown, null, 0);
            SearchText = string.Empty;

            InitializeComponent();
            commandService = service;

            Autoscroll = optionsProvider.Autoscroll;
            FunctionListSortState = optionsProvider.SortOptions;
            optionsProvider.OptionsUpdated += OptionsUpdated;
        }

        private void OptionsUpdated(OptionsProvider sender)
        {
            try
            {
                Autoscroll = sender.Autoscroll;
                FunctionListSortState = sender.SortOptions;
                SortAndReloadFunctionList();
            }
            catch (Exception e)
            {
                Error.LogError(e);
            }
        }

        public async Task UpdateFunctionListAsync(ITextSnapshot textSnapshot, IEnumerable<FunctionListItem> newTokens)
        {
            if (textSnapshot == CurrentVersion)
                return;

            CurrentVersion = textSnapshot;
            Tokens = newTokens.ToList();
            Helper.SortAndFilter(Tokens, FunctionListSortState, SearchText);
            
            await AddTokensToViewAsync(Tokens);
            await HighlightCurrentFunctionAsync(LastHighlightedToken.Type, LastHighlightedToken.LineNumber);
        }

        public async Task ClearHighlightCurrentFunctionAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            LastHighlightedToken.IsCurrentWorkingItem = false;
        }

        public async Task HighlightCurrentFunctionAsync(RadAsmTokenType tokenType, int lineNumber)
        {
            var value = Tokens.FirstOrDefault(t => t.Type == tokenType && t.LineNumber == lineNumber);

            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            LastHighlightedToken.IsCurrentWorkingItem = false;
            if (value == null)
                return;

            value.IsCurrentWorkingItem = true;
            LastHighlightedToken = value;

            if (Autoscroll)
                tokens.ScrollIntoView(value);
        }

        private void SortAndReloadFunctionList()
        {
            var filteredTokens = Helper.SortAndFilter(Tokens, FunctionListSortState, SearchText);
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

            ResizeFunctionListColumns();
        }

        private void ByNumber_Click(object sender, RoutedEventArgs e)
        {
            FunctionListSortState = FunctionListSortState != SortState.ByLine 
                ? SortState.ByLine 
                : SortState.ByLineDescending;

            SortAndReloadFunctionList();
        }

        private void ByName_Click(object sender, RoutedEventArgs e)
        {
            FunctionListSortState = FunctionListSortState != SortState.ByName
                ? SortState.ByNameDescending
                : SortState.ByName;

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
                Point p = this.PointToScreen(e.GetPosition(this));
                commandService.ShowContextMenu(menuID, (int)p.X, (int)p.Y);
            }
        }

        private void ShowHideLineNumber(object sender, EventArgs e)
        {
            if (isHideLineNumber)
                isHideLineNumber = false;
            else
                isHideLineNumber = true;

            ResizeFunctionListColumns();
        }

        private void Search_TextChanged(object sender, TextChangedEventArgs e)
        {
            SearchText = Search.Text;
            var filteredTokens = Helper.Filter(Tokens, SearchText);
            ReloadFunctionList(filteredTokens);
        }

        private void ResizeFunctionListColumns()
        {
            functionsGridView.Columns[0].Width = isHideLineNumber ? 0 : double.NaN;

            functionsGridView.Columns[1].Width = 0;
            functionsGridView.Columns[1].Width = double.NaN;

            tokens.UpdateLayout();
            LineNumberButtonColumn.Width = new GridLength(functionsGridView.Columns[0].ActualWidth);
        }

        public void OnClearSearchField() => Search.Text = "";

        public void GoToSelectedItem()
        {
            try
            {
                var token = (FunctionListItem)tokens.SelectedItem;
                FunctionList.Instance
                    .GetActiveTextView()
                    .ChangeCaretPosition(token.LineNumber - 1);
            }
            catch (Exception e)
            {
                Error.LogError(e);
            }
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