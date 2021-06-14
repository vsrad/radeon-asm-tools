using System.ComponentModel;
using System.Windows.Data;
using Microsoft.VisualStudio.Shell;
using VSRAD.Syntax.Helpers;
using VSRAD.Syntax.Options;

namespace VSRAD.Syntax.FunctionList
{
    public sealed class FunctionListContext : DefaultNotifyPropertyChanged
    {
        private bool _showLineColumn;

        public bool ShowLineColumn
        {
            get => _showLineColumn;
            set 
            {
                OnPropertyChanged(ref _showLineColumn, value);
                OnFilterChanged();
            }
        }

        private FilterTypeState _filterType;
        public FilterTypeState FilterType
        {
            get => _filterType;
            set
            {
                OnPropertyChanged(ref _filterType, value);
                OnFilterChanged();
            }
        }

        private string _filterText;
        public string FilterText
        {
            get => _filterText;
            set
            {
                OnPropertyChanged(ref _filterText, value);
                OnFilterChanged();
            }
        }

        public FunctionListItem SelectedItem
        {
            get => _model.SelectedItem;
            set => _model.SelectedItem = value;
        }

        public bool AutoScroll => _model.AutoScroll;

        private readonly CollectionViewSource _viewItems;
        public ICollectionView ViewItems => _viewItems.View;


        private ListSortDirection _numberSortDirection;
        private ListSortDirection _textSortDirection;
        private readonly FunctionListModel _model;

        public FunctionListContext()
        {
            _model = FunctionListModel.CurrentModel;
            _filterType = FilterTypeState.FL;
            _showLineColumn = true;

            _viewItems = new CollectionViewSource();
            _viewItems.Source = _model.Items;
            _viewItems.Filter += ApplyFilter;

            SortStateToFilter(_model.SortState);
            _model.PropertyChanged += ModelPropertyChanged;

            LineSortCommand = new NoParameterCommand(() => 
                ApplySort(nameof(FunctionListItem.LineNumber), ref _numberSortDirection));

            TextSortCommand = new NoParameterCommand(() =>
                ApplySort(nameof(FunctionListItem.Text), ref _textSortDirection));

            ChangeLineVisibilityCommand = new NoParameterCommand(() =>
                ShowLineColumn = !ShowLineColumn);

            NavigateToCurrentItemCommand = new NoParameterCommand(() =>
                _model.NavigateToItem(SelectedItem));

            ChangeFilterTypeCommand = new NoParameterCommand(ChangeFilterState);
            ClearFilterTextCommand = new NoParameterCommand(() => FilterText = "");
        }

        private void ModelPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(FunctionListModel.Items):
                    _viewItems.View.Refresh();
                    break;
                case nameof(FunctionListModel.SortState):
                    SortStateToFilter(_model.SortState);
                    break;
                default:
                    RaisePropertyChanged(e.PropertyName);
                    break;
            }
        }

        public INoParameterCommand LineSortCommand { get; }
        public INoParameterCommand TextSortCommand { get; }
        public INoParameterCommand ChangeFilterTypeCommand { get; }
        public INoParameterCommand ClearFilterTextCommand { get; }
        public INoParameterCommand ChangeLineVisibilityCommand { get; }
        public INoParameterCommand NavigateToCurrentItemCommand { get; }

        private void ChangeFilterState()
        {
            FilterType = FilterType == FilterTypeState.FL
                ? FilterTypeState.F
                : FilterType == FilterTypeState.F
                    ? FilterTypeState.L
                    : FilterTypeState.FL;
        }

        private void ApplySort(string propertyName, ref ListSortDirection sortDirection)
        {
            var newValue = sortDirection == ListSortDirection.Ascending
                ? ListSortDirection.Descending
                : ListSortDirection.Ascending;

            ApplySort(propertyName, ref sortDirection, newValue);
        }

        private void SortStateToFilter(GeneralOptionPage.SortState sortState)
        {
            switch (sortState)
            {
                case GeneralOptionPage.SortState.ByLine:
                    ApplySort(nameof(FunctionListItem.LineNumber), ref _numberSortDirection, ListSortDirection.Ascending); break;
                case GeneralOptionPage.SortState.ByLineDescending:
                    ApplySort(nameof(FunctionListItem.LineNumber), ref _numberSortDirection, ListSortDirection.Descending); break;
                case GeneralOptionPage.SortState.ByName:
                    ApplySort(nameof(FunctionListItem.Text), ref _textSortDirection, ListSortDirection.Ascending); break;
                case GeneralOptionPage.SortState.ByNameDescending:
                    ApplySort(nameof(FunctionListItem.Text), ref _textSortDirection, ListSortDirection.Descending); break;
            }
        }

        private void ApplySort(string propertyName, ref ListSortDirection sortDirection, ListSortDirection value)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            sortDirection = value;

            _viewItems.SortDescriptions.Clear();
            _viewItems.SortDescriptions.Add(new SortDescription(propertyName, sortDirection));
        }

        private void ApplyFilter(object sender, FilterEventArgs e)
        {
            var item = (FunctionListItem)e.Item;

            var isText = string.IsNullOrWhiteSpace(_filterText) || item.Text.Contains(_filterText);
            var isType = (FilterType == FilterTypeState.FL)
                         || (item.Type == FunctionListItemType.Label && FilterType == FilterTypeState.L)
                         || (item.Type == FunctionListItemType.Function && FilterType == FilterTypeState.F);

            e.Accepted = isType && isText;
        }

        private void OnFilterChanged() =>
            _viewItems.View.Refresh();
    }

    public enum FilterTypeState
    {
        FL = 1, // functions and labels
        F = 2, // only functions
        L = 3, // only labels
    }
}
