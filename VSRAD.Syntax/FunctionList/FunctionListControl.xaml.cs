using VSRAD.Syntax.Parser;
using VSRAD.Syntax.Parser.Blocks;
using VSRAD.Syntax.Helpers;
using VSRAD.Syntax.FunctionList.Commands;
using static VSRAD.Syntax.Options.OptionPage;
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
        public static FunctionListControl Instance { get; private set; }
        private readonly OleMenuCommandService commandService;
        private bool isHideLineNumber = false;
        private SortState FunctionListSortState = SortState.ByName;

        public IList<IBaseBlock> Functions { get; private set; }

        public FunctionListControl(OleMenuCommandService service)
        {
            var showHideLineNumberCommand = new CommandID(FunctionListCommand.CommandSet, Constants.ShowHideLineNumberCommandId);
            service.AddCommand(new MenuCommand(ShowHideLineNumber, showHideLineNumberCommand));

            this.InitializeComponent();
            this.commandService = service;
            this.Loaded += OnInitializedFunctionList;
            FunctionListControl.Instance = this;
        }

        public void ReloadFunctionList()
        {
            var functionList = this.Functions;
            if (functionList != null)
                AddFunctionsToView(functionList);
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

        internal static async Task UpdateFunctionListAsync(object sender)
        {
            if (sender == null)
                return;

            IList<IBaseBlock> newFunctions = (sender as BaseParser).GetFunctionBlocks();
            if (FunctionListControl.Instance != null && newFunctions != null)
            {
                FunctionListControl.Instance.Functions = newFunctions;

                var shownFunctions = FunctionListControl.Instance.SearchByNameFilter(newFunctions);

                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                FunctionListControl.Instance.AddFunctionsToView(shownFunctions);
                FunctionListControl.Instance.ResizeFunctionListColumns();
            }
        }

        internal static void OnChangeOptions(SortState option)
        {
            if (FunctionListControl.Instance != null)
            {
                FunctionListControl.Instance.FunctionListSortState = option;
                FunctionListControl.Instance.ReloadFunctionList();
            }
        }

        private void AddFunctionsToView(IEnumerable<IBaseBlock> functionList)
        {
            switch (FunctionListSortState)
            {
                case SortState.ByLine:
                    functionList = functionList
                        .OrderBy(func => (func as FunctionBlock).FunctionToken.LineNumber)
                        .ToList();
                    break;

                case SortState.ByName:
                    functionList = functionList
                        .OrderBy(func => (func as FunctionBlock).FunctionToken.TokenName)
                        .ToList();
                    break;

                case SortState.ByLineDescending:
                    functionList = functionList
                        .OrderByDescending(func => (func as FunctionBlock).FunctionToken.LineNumber)
                        .ToList();
                    break;

                case SortState.ByNameDescending:
                    functionList = functionList
                        .OrderByDescending(func => (func as FunctionBlock).FunctionToken.TokenName)
                        .ToList();
                    break;
                default:
                    functionList = functionList
                        .OrderBy(func => (func as FunctionBlock).FunctionToken.LineNumber)
                        .ToList();
                    break;
            }

            functions.Items.Clear();
            foreach (var func in functionList)
                functions.Items.Add(func);
        }

        private void OnInitializedFunctionList(object sender, object args)
        {
            if (Package.Instance != null)
                this.FunctionListSortState = Package.Instance.OptionPage.SortOptions;
        }

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

        private IEnumerable<IBaseBlock> SearchByNameFilter(IEnumerable<IBaseBlock> functionList)
        {
            if (functionList == null)
                return new List<IBaseBlock>();

            return functionList
                .Where(fun => (fun as FunctionBlock).FunctionToken.TokenName.Contains(Search.Text));
        }

        private void Search_TextChanged(object sender, TextChangedEventArgs e)
        {
            var filteredFunctions = SearchByNameFilter(this.Functions);
            AddFunctionsToView(filteredFunctions);

            ResizeFunctionListColumns();
        }

        private void ResizeFunctionListColumns()
        {
            if (isHideLineNumber)
                this.functionsGridView.Columns[0].Width = 0;
            else
                this.functionsGridView.Columns[0].Width = Double.NaN;

            this.functionsGridView.Columns[1].Width = 0;
            this.functionsGridView.Columns[1].Width = Double.NaN;

            this.functions.UpdateLayout();
            this.LineNumberButtonColumn.Width = new GridLength(this.functionsGridView.Columns[0].ActualWidth);
        }

        public void OnClearSearchField() => this.Search.Text = "";

        public void GoToSelectedItem()
        {
            if (functions.SelectedItem is FunctionBlock function)
                FunctionList.Instance?.GetWpfTextView()?.ChangeCaretPosition(function.FunctionToken.Line);
        }

        private void FunctionListContentGridOnLoad(object sender, RoutedEventArgs e) => functionListContentGrid.Focus();

        private void FunctionListContentGrid_KeyDown(object sender, KeyEventArgs e) => Keyboard.Focus(Search);

        private void Search_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Up || e.Key == Key.Down)
                Keyboard.Focus(functions);
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