using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using VSRAD.Package.Options;
using VSRAD.Package.Utils;

namespace VSRAD.Package.ProjectSystem.Macros
{
    // Items displayed in WPF designer (replaced with user data at run time)
    public sealed class MacroListDesignTimeCollection : ObservableCollection<MacroItem>
    {
        public MacroListDesignTimeCollection()
        {
            Add(new MacroItem("RadBreakLine", "<next breakpoint line>", userDefined: false));
            Add(new MacroItem("RadDeployDir", "/home/rad/deploy", userDefined: true));
            Add(new MacroItem("RadDebugOutputPath", "$(RadDeployDir)/out.bin", userDefined: true));
        }
    }

    public sealed class MacroListDisplayCollection : ObservableCollection<MacroItem>
    {
        // Reset the collection when removing an item to clear DataGrid validation errors, see https://stackoverflow.com/q/9381847
        protected override void RemoveItem(int index)
        {
            base.RemoveItem(index);
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }
    }

    public partial class MacroListEditor : UserControl
    {
        private ObservableCollection<MacroItem> _sourceCollection;

        public ICommand AddMacroCommand { get; }
        public ICommand DeleteMacroCommand { get; }

        public MacroListEditor()
        {
            InitializeComponent();
            AddMacroCommand = new WpfDelegateCommand(_ => ((MacroListDisplayCollection)DataContext).Add(new MacroItem()));
            DeleteMacroCommand = new WpfDelegateCommand(item => ((MacroListDisplayCollection)DataContext).Remove((MacroItem)item));

            if (DataContext != null)
                // DataContext is immediately initialized in WPF designer
                SetupDisplayCollection(DataContext);
            else
                // DataContext is bound after initialization at run time
                DataContextChanged += (s, e) => SetupDisplayCollection(e.NewValue);
        }

        private void SetupDisplayCollection(object dataContext)
        {
            // Merge user-defined macros (DataContext) with predefined ones into a new "display" collection
            if (_sourceCollection == null && dataContext is ObservableCollection<MacroItem> sourceCollection)
            {
                _sourceCollection = sourceCollection;

                var displayItems = new MacroListDisplayCollection
                {
                    new MacroItem("RadBreakLine", "<next breakpoint line>", userDefined: false),
                    new MacroItem("RadWatches", "<visualizer watches, comma-separated>", userDefined: false)
                };

                foreach (var item in _sourceCollection)
                    displayItems.Add(item);

                // Sync added/removed macros with the source collection
                displayItems.CollectionChanged += DisplayItemsChanged;
                DataContext = displayItems;
            }
        }

        private void DisplayItemsChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Remove)
            {
                foreach (MacroItem item in e.OldItems)
                    _sourceCollection.Remove(item);
            }
            else if (e.Action == NotifyCollectionChangedAction.Add)
            {
                foreach (MacroItem item in e.NewItems)
                    _sourceCollection.Add(item);

                if (e.NewItems.Count == 1 && e.NewItems[0] is MacroItem addedItem)
                {
                    MacroGrid.SelectedItem = addedItem;
                    MacroGrid.CurrentCell = new DataGridCellInfo(addedItem, MacroGrid.Columns[0]);
#pragma warning disable VSTHRD001 // Use BeginInvoke to focus on the added macro item _after_ it's been added to the DataGrid
                    Dispatcher.BeginInvoke((Action)(() => MacroGrid.BeginEdit()), DispatcherPriority.Background);
#pragma warning restore VSTHRD001
                }
            }
        }
    }
}

