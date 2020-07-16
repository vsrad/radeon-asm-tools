using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace VSRAD.Package.ProjectSystem.Macros
{
    public sealed partial class MacroEditorWindow : Window
    {
        private MacroEditContext Editor => (MacroEditContext)DataContext;

        private bool _promptUnsavedOnClose = true;

        public MacroEditorWindow(MacroEditContext macroEditor)
        {
            DataContext = macroEditor;
            InitializeComponent();
        }

        private void InsertMacro(object sender, MouseButtonEventArgs e)
        {
            if (((ListView)sender).SelectedItem is KeyValuePair<string, string> macro)
                Editor.MacroValue = Editor.MacroValue.Insert(MacroInput.CaretIndex, macro.Key);
        }

        private void HandleOK(object sender, RoutedEventArgs e)
        {
            _promptUnsavedOnClose = false;
            Close();
        }

        private void HandleCancel(object sender, RoutedEventArgs e)
        {
            _promptUnsavedOnClose = false;
            Editor.ResetChanges();
            Close();
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            if (_promptUnsavedOnClose && Editor.MacroValueChanged)
            {
                var result = MessageBox.Show($"Save changes to {Editor.MacroName}?", "Macro Editor", MessageBoxButton.YesNo);
                if (result == MessageBoxResult.No)
                    Editor.ResetChanges();
            }

            base.OnClosing(e);
        }
    }
}
