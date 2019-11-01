using System.Collections.Generic;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;

namespace VSRAD.Package.ProjectSystem.Macros
{
    public partial class MacroEditorWindow : Window
    {
        private MacroEditor Editor => (MacroEditor)DataContext;

        public MacroEditorWindow(MacroEditor macroEditor)
        {
            DataContext = macroEditor;
            InitializeComponent();

            listMacros.MouseDoubleClick += InsertMacro;
            ((CollectionView)CollectionViewSource.GetDefaultView(listMacros.ItemsSource)).Filter = FilterMacro;
            Editor.MacroPreviewFilter = "rad";
        }

        private void InsertMacro(object sender, MouseButtonEventArgs e)
        {
            var macro = (listMacros.SelectedItem as KeyValuePair<string, string>?)?.Key;
            if (macro != null)
                Editor.MacroValue = Editor.MacroValue.Insert(textCommand.CaretIndex, macro);
        }

        private bool FilterMacro(object macro)
        {
            if (string.IsNullOrEmpty(Editor.MacroPreviewFilter)) return true;

            var macroData = ((KeyValuePair<string, string>)macro);
            return macroData.Key.IndexOf(Editor.MacroPreviewFilter, System.StringComparison.OrdinalIgnoreCase) != -1
                || macroData.Value.IndexOf(Editor.MacroPreviewFilter, System.StringComparison.OrdinalIgnoreCase) != -1;
        }

        private void MacroFilterChanged(object sender, System.Windows.Controls.TextChangedEventArgs e) =>
            CollectionViewSource.GetDefaultView(listMacros.ItemsSource).Refresh();
    }
}
