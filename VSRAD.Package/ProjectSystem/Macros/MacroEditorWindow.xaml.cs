using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace VSRAD.Package.ProjectSystem.Macros
{
    public sealed partial class MacroEditorWindow : Window
    {
        private MacroEditor Editor => (MacroEditor)DataContext;

        public MacroEditorWindow(MacroEditor macroEditor)
        {
            DataContext = macroEditor;
            InitializeComponent();
        }

        private void InsertMacro(object sender, MouseButtonEventArgs e)
        {
            if (((ListView)sender).SelectedItem is KeyValuePair<string, string> macro)
                Editor.MacroValue = Editor.MacroValue.Insert(textCommand.CaretIndex, macro.Key);
        }
    }
}
