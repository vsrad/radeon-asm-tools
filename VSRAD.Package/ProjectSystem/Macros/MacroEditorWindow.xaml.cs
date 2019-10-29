using System.Collections.Generic;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using VSRAD.Package.Utils;

namespace VSRAD.Package.ProjectSystem.Macros
{
    public partial class MacroEditorWindow : Window
    {
        public sealed class Context : DefaultNotifyPropertyChanged
        {
            public string MacroName { get; set; }

            private string _macroValue;
            public string MacroValue
            {
                get => _macroValue;
                set
                {
                    SetField(ref _macroValue, value);
                    RaisePropertyChanged(nameof(EvaluatedValue));
                }
            }

            public string EvaluatedValue => _evaluateMacro(_macroValue);
            public Dictionary<string, string> EnvironmentMacros { get; set; }

            private string _macroFilter;
            public string MacroFilter
            {
                get => _macroFilter;
                set => SetField(ref _macroFilter, value);
            }

            private readonly EvaluateMacroDelegate _evaluateMacro;

            public Context(EvaluateMacroDelegate evaluateMacro) => _evaluateMacro = evaluateMacro;
        }

        public delegate string EvaluateMacroDelegate(string unevaluatedValue);

        public string Value => ((Context)DataContext).MacroValue;

        public MacroEditorWindow(
            string macroName,
            string macroValue,
            Dictionary<string, string> environmentMacros,
            EvaluateMacroDelegate evaluateMacro)
        {
            DataContext = new Context(evaluateMacro) { MacroName = macroName, MacroValue = macroValue, EnvironmentMacros = environmentMacros };
            InitializeComponent();

            listMacros.MouseDoubleClick += InsertMacro;
            ((CollectionView)CollectionViewSource.GetDefaultView(listMacros.ItemsSource)).Filter = FilterMacro;
            ((Context)DataContext).MacroFilter = "rad";
        }

        private void InsertMacro(object sender, MouseButtonEventArgs e)
        {
            var macro = (listMacros.SelectedItem as KeyValuePair<string, string>?)?.Key;
            if (macro != null)
                ((Context)DataContext).MacroValue = ((Context)DataContext).MacroValue.Insert(textCommand.CaretIndex, macro);
        }

        private bool FilterMacro(object macro)
        {
            if (string.IsNullOrEmpty(((Context)DataContext).MacroFilter)) return true;

            var macroData = ((KeyValuePair<string, string>)macro);
            return macroData.Key.IndexOf(((Context)DataContext).MacroFilter, System.StringComparison.OrdinalIgnoreCase) != -1
                || macroData.Value.IndexOf(((Context)DataContext).MacroFilter, System.StringComparison.OrdinalIgnoreCase) != -1;
        }

        private void MacroFilterChanged(object sender, System.Windows.Controls.TextChangedEventArgs e) =>
            CollectionViewSource.GetDefaultView(listMacros.ItemsSource).Refresh();
    }
}
