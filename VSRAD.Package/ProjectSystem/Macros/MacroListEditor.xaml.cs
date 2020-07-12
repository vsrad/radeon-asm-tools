using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
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
        public void RefreshCollection() =>
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));

        // Reset the collection when removing an item to clear DataGrid validation errors, see https://stackoverflow.com/q/9381847
        protected override void RemoveItem(int index)
        {
            base.RemoveItem(index);
            RefreshCollection();
        }
    }

    public partial class MacroListEditor : UserControl
    {
        public static MacroListDisplayCollection GetPredefinedMacroCollection() => new MacroListDisplayCollection
        {
            new MacroItem(RadMacros.ActiveSourceFile, "<current source file>", userDefined: false),
            new MacroItem(RadMacros.ActiveSourceFileLine, "<line number under the cursor>", userDefined: false),
            new MacroItem(RadMacros.Watches, "<visualizer watches, colon-separated>", userDefined: false),
            new MacroItem(RadMacros.BreakLine, "<next breakpoint line(s), colon-separated>", userDefined: false),
            new MacroItem(RadMacros.DebugAppArgs, "<app args, set in visualizer>", userDefined: false),
            new MacroItem(RadMacros.DebugBreakArgs, "<break args, set in visualizer>", userDefined: false),
            new MacroItem(RadMacros.Counter, "<counter, set in visualizer>", userDefined: false),
            new MacroItem(RadMacros.NGroups, "<ngroups, set in visualizer>", userDefined: false)
        };

        public ICommand AddMacroCommand { get; }
        public ICommand DeleteMacroCommand { get; }
        public ICommand RichEditCommand { get; }

        private string _macroFilter = "";
        public string MacroFilter
        {
            get => _macroFilter;
            set
            {
                _macroFilter = value;
                ((MacroListDisplayCollection)DataContext).RefreshCollection();
            }
        }

        public DirtyProfileMacroEditor MacroEditor
        {
            get => (DirtyProfileMacroEditor)GetValue(MacroEditorProperty); set => SetValue(MacroEditorProperty, value);
        }

        public static readonly DependencyProperty MacroEditorProperty =
            DependencyProperty.Register(nameof(MacroEditor), typeof(DirtyProfileMacroEditor), typeof(MacroListEditor), new PropertyMetadata(null));

        private ObservableCollection<MacroItem> _sourceCollection;

        public MacroListEditor()
        {
            InitializeComponent();
            AddMacroCommand = new WpfDelegateCommand(_ => ((MacroListDisplayCollection)DataContext).Add(new MacroItem()));
            DeleteMacroCommand = new WpfDelegateCommand(item => ((MacroListDisplayCollection)DataContext).Remove((MacroItem)item));
            RichEditCommand = new WpfDelegateCommand(OpenMacroEditor);

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

                var displayItems = GetPredefinedMacroCollection();
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

        private void OpenMacroEditor(object param)
        {
            var item = (MacroItem)param;
            VSPackage.TaskFactory.RunAsyncWithErrorHandling(async () =>
                item.Value = await MacroEditor.EditAsync(item.Name, item.Value));
        }

        private void FilterMacros(object sender, FilterEventArgs e)
        {
            var macroItem = (MacroItem)e.Item;
            e.Accepted = string.IsNullOrEmpty(MacroFilter)
                || string.IsNullOrEmpty(macroItem.Name) // always show newly added macros (without name)
                || macroItem.Name.IndexOf(MacroFilter, StringComparison.OrdinalIgnoreCase) != -1
                || macroItem.Value.IndexOf(MacroFilter, StringComparison.OrdinalIgnoreCase) != -1;
        }
    }
}

