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
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace VSRAD.Syntax.FunctionList
{
    public class FunctionListToken : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public RadAsmTokenType Type { get; }
        public string Text { get; }
        public int LineNumber { get; }

        private bool _isCurrentWorkingItem;
        public bool IsCurrentWorkingItem
        {
            get => _isCurrentWorkingItem;
            set => OnPropertyChanged(ref _isCurrentWorkingItem, value);
        }

        public FunctionListToken(RadAsmTokenType type, string text, int lineNumber)
        {
            Type = type;
            Text = text;
            LineNumber = lineNumber + 1;
            _isCurrentWorkingItem = false;
        }

        private void OnPropertyChanged<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
                return;

            field = value;
            RaisePropertyChanged(propertyName);
        }

        private void RaisePropertyChanged(string propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    }

    public partial class FunctionListControl : UserControl
    {
        private readonly OleMenuCommandService commandService;
        private bool isHideLineNumber = false;
        private SortState FunctionListSortState;
        private List<FunctionListToken> Tokens;
        private FunctionListToken LastHighlightedToken;
        private ITextSnapshot CurrentVersion;

        public FunctionListControl(OleMenuCommandService service, OptionsProvider optionsProvider)
        {
            var showHideLineNumberCommand = new CommandID(FunctionListCommand.CommandSet, Constants.ShowHideLineNumberCommandId);
            service.AddCommand(new MenuCommand(ShowHideLineNumber, showHideLineNumberCommand));
            Tokens = new List<FunctionListToken>();
            LastHighlightedToken = new FunctionListToken(RadAsmTokenType.Unknown, null, 0);

            InitializeComponent();
            commandService = service;

            FunctionListSortState = optionsProvider.SortOptions;
            optionsProvider.OptionsUpdated += SortOptionsUpdated;
        }

        private void SortOptionsUpdated(OptionsProvider sender)
        {
            try
            {
                FunctionListSortState = sender.SortOptions;
                ReloadFunctionList();
            }
            catch (Exception e)
            {
                Error.LogError(e);
            }
        }

        public Task UpdateFunctionListAsync(ITextSnapshot textSnapshot, IEnumerable<FunctionListToken> newTokens)
        {
            try
            {
                if (textSnapshot == CurrentVersion)
                    return Task.CompletedTask;
                CurrentVersion = textSnapshot;

                Tokens = newTokens.ToList();
                var shownTokens = SearchByNameFilter(newTokens);

                return AddTokensToViewAsync(shownTokens);
            }
            catch (Exception e)
            {
                Error.LogError(e);
                return Task.CompletedTask;
            }
        }

        public async Task HighlightCurrentFunctionAsync(RadAsmTokenType tokenType, int lineNumber)
        {
            try
            {
                var value = Tokens.FirstOrDefault(t => t.Type == tokenType && t.LineNumber == lineNumber);
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                LastHighlightedToken.IsCurrentWorkingItem = false;
                if (value == null)
                    return;

                value.IsCurrentWorkingItem = true;
                LastHighlightedToken = value;
            }
            catch (Exception e)
            {
                Error.LogError(e);
            }
        }

        private void ReloadFunctionList() =>
            ThreadHelper.JoinableTaskFactory.RunAsync(() => AddTokensToViewAsync(Tokens));

        private async Task AddTokensToViewAsync(IEnumerable<FunctionListToken> shownTokens)
        {
            switch (FunctionListSortState)
            {
                case SortState.ByLine:
                    shownTokens = shownTokens
                        .OrderBy(token => token.LineNumber);
                    break;

                case SortState.ByName:
                    shownTokens = shownTokens
                        .OrderBy(token => token.Text, StringComparer.OrdinalIgnoreCase);
                    break;

                case SortState.ByLineDescending:
                    shownTokens = shownTokens
                        .OrderByDescending(token => token.LineNumber);
                    break;

                case SortState.ByNameDescending:
                    shownTokens = shownTokens
                        .OrderByDescending(token => token.Text, StringComparer.OrdinalIgnoreCase);
                    break;
                default:
                    shownTokens = shownTokens
                        .OrderBy(token => token.LineNumber);
                    break;
            }

            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            tokens.Items.Clear();
            foreach (var token in shownTokens)
                tokens.Items.Add(token);

            ResizeFunctionListColumns();
        }

        private void ByNumber_Click(object sender, RoutedEventArgs e)
        {
            switch (FunctionListSortState)
            {
                case SortState.ByLine:
                    FunctionListSortState = SortState.ByLineDescending;
                    break;
                default:
                    FunctionListSortState = SortState.ByLine;
                    break;
            }
            ReloadFunctionList();
        }

        private void ByName_Click(object sender, RoutedEventArgs e)
        {
            switch (FunctionListSortState)
            {
                case SortState.ByName:
                    FunctionListSortState = SortState.ByNameDescending;
                    break;
                default:
                    FunctionListSortState = SortState.ByName;
                    break;
            }
            ReloadFunctionList();
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

        private IEnumerable<FunctionListToken> SearchByNameFilter(IEnumerable<FunctionListToken> newTokens) =>
            newTokens.Where(t => t.Text.Contains(Search.Text));

        private void Search_TextChanged(object sender, TextChangedEventArgs e)
        {
            var filteredTokens = SearchByNameFilter(Tokens);
            ThreadHelper.JoinableTaskFactory.RunAsync(() => AddTokensToViewAsync(filteredTokens));
        }

        private void ResizeFunctionListColumns()
        {
            if (isHideLineNumber)
                functionsGridView.Columns[0].Width = 0;
            else
                functionsGridView.Columns[0].Width = Double.NaN;

            functionsGridView.Columns[1].Width = 0;
            functionsGridView.Columns[1].Width = Double.NaN;

            tokens.UpdateLayout();
            LineNumberButtonColumn.Width = new GridLength(this.functionsGridView.Columns[0].ActualWidth);
        }

        public void OnClearSearchField() => Search.Text = "";

        public void GoToSelectedItem()
        {
            try
            {
                var token = (FunctionListToken)tokens.SelectedItem;
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