using System.Collections.ObjectModel;
using System.Windows.Controls;
using VSRAD.Package.Options;

namespace VSRAD.Package.ProjectSystem.Macros
{
    public sealed class MacroListDesignTimeCollection : ObservableCollection<MacroItem>
    {
        public MacroListDesignTimeCollection()
        {
            Add(new MacroItem("RadBreakLine", "<next breakpoint line>", predefined: true));
            Add(new MacroItem("RadDeployDir", "/home/rad/deploy", predefined: false));
            Add(new MacroItem("RadDebugOutputPath", "$(RadDeployDir)/out.bin", predefined: false));
        }
    }

    public partial class MacroListEditor : UserControl
    {
        private ObservableCollection<MacroItem> _sourceCollection;

        public MacroListEditor()
        {
            InitializeComponent();
            if (DataContext != null)
                SetupDisplayCollection(DataContext);
            else
                DataContextChanged += (s, e) => SetupDisplayCollection(e.NewValue);
        }

        private void SetupDisplayCollection(object dataContext)
        {
            if (_sourceCollection == null && dataContext is ObservableCollection<MacroItem> sourceCollection)
            {
                _sourceCollection = sourceCollection;

                var displayItems = new ObservableCollection<MacroItem>();
                displayItems.Add(new MacroItem("RadBreakLine", "<next breakpoint line>", predefined: true));
                displayItems.Add(new MacroItem("RadWatches", "<visualizer watches, comma-separated>", predefined: true));
                foreach (var item in _sourceCollection)
                    displayItems.Add(item);

                DataContext = displayItems;
            }
        }
    }
}

