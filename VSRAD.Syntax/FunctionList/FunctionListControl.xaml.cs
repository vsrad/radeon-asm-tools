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
using Microsoft;

namespace VSRAD.Syntax.FunctionList
{
    public partial class FunctionListControl : UserControl
    {
        private readonly OleMenuCommandService commandService;
        private bool isHideLineNumber = false;
        private SortState FunctionListSortState = SortState.ByName;
        private IList<FunctionBlock> Functions;

        public FunctionListControl(OleMenuCommandService service)
        {
            var showHideLineNumberCommand = new CommandID(FunctionListCommand.CommandSet, Constants.ShowHideLineNumberCommandId);
            service.AddCommand(new MenuCommand(ShowHideLineNumber, showHideLineNumberCommand));

            this.InitializeComponent();
            this.commandService = service;
            this.Loaded += OnInitializedFunctionList;
        }

        public async Task UpdateFunctionListAsync(IEnumerable<FunctionBlock> newFunctions)
        {
            try
            {
                Functions = newFunctions.ToList();

                var shownFunctions = SearchByNameFilter(newFunctions);

                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                AddFunctionsToView(shownFunctions);
                ResizeFunctionListColumns();
            }
            catch (Exception e)
            {
                Error.LogError(e);
            }
        }

        public void ChangeSortOptions(SortState option)
        {
            try
            {
                FunctionListSortState = option;
                ReloadFunctionList();
            }
            catch (Exception e)
            {
                Error.LogError(e);
            }
        }

        private void ReloadFunctionList() => AddFunctionsToView(Functions);

        private void AddFunctionsToView(IEnumerable<FunctionBlock> functionList)
        {
            switch (FunctionListSortState)
            {
                case SortState.ByLine:
                    functionList = functionList
                        .OrderBy(func => func.FunctionToken.LineNumber)
                        .ToList();
                    break;

                case SortState.ByName:
                    functionList = functionList
                        .OrderBy(func => func.FunctionToken.TokenName)
                        .ToList();
                    break;

                case SortState.ByLineDescending:
                    functionList = functionList
                        .OrderByDescending(func => func.FunctionToken.LineNumber)
                        .ToList();
                    break;

                case SortState.ByNameDescending:
                    functionList = functionList
                        .OrderByDescending(func => func.FunctionToken.TokenName)
                        .ToList();
                    break;
                default:
                    functionList = functionList
                        .OrderBy(func => func.FunctionToken.LineNumber)
                        .ToList();
                    break;
            }

            functions.Items.Clear();
            foreach (var func in functionList)
                functions.Items.Add(func);
        }

        private void OnInitializedFunctionList(object sender, object args)
            => FunctionListSortState = Package.Instance.OptionPage.SortOptions;

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

        private IEnumerable<FunctionBlock> SearchByNameFilter(IEnumerable<FunctionBlock> functionList)
        {
            if (functionList == null)
                return Enumerable.Empty<FunctionBlock>();

            return functionList
                .Where(fun => fun.FunctionToken.TokenName.Contains(Search.Text));
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
            try
            {
                var function = (FunctionBlock)functions.SelectedItem;
                FunctionList.Instance.GetActiveTextView().ChangeCaretPosition(function.FunctionToken.Line);
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