using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using VSRAD.Syntax.Helpers;
using VSRAD.Syntax.Options;

namespace VSRAD.Syntax.FunctionList
{
    public class FunctionListModel : DefaultNotifyPropertyChanged
    {
        public static readonly FunctionListModel CurrentModel = new FunctionListModel();

        private GeneralOptionPage.SortState _sortState;
        public GeneralOptionPage.SortState SortState
        {
            get => _sortState;
            set => OnPropertyChanged(ref _sortState, value);
        }

        private FunctionListItem _selectedItem;
        public FunctionListItem SelectedItem
        {
            get => _selectedItem;
            set => OnPropertyChanged(ref _selectedItem, value);
        }

        private bool _autoScroll;
        public bool AutoScroll
        {
            get => _autoScroll;
            set => OnPropertyChanged(ref _autoScroll, value);
        }

        private readonly RangeObservableCollection<FunctionListItem> _items;
        public readonly ReadOnlyObservableCollection<FunctionListItem> Items;

        public FunctionListModel()
        {
            OptionsUpdated(GeneralOptionProvider.Instance);
            GeneralOptionProvider.Instance.OptionsUpdated += OptionsUpdated;

            _items = new RangeObservableCollection<FunctionListItem>();
            Items = new ReadOnlyObservableCollection<FunctionListItem>(_items);

            _items.CollectionChanged += (s, e) => RaisePropertyChanged(nameof(Items));
        }

        private void OptionsUpdated(GeneralOptionProvider sender)
        {
            if (sender.SortOptions != SortState)
                SortState = sender.SortOptions;
            if (sender.AutoScroll != AutoScroll)
                AutoScroll = sender.AutoScroll;
        }

        public void UpdateItems(IEnumerable<FunctionListItem> items) =>
            _items.ReplaceRange(items);

        public void NavigateToItem(FunctionListItem item) =>
            item?.Navigate();

        public class RangeObservableCollection<T> : ObservableCollection<T>
        {
            public void ReplaceRange(IEnumerable<T> items)
            {
                CheckReentrancy();

                Items.Clear();
                foreach (var item in items)
                    Items.Add(item);

                OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
            }
        }
    }
}
