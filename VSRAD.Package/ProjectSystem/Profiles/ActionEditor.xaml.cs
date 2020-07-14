using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using VSRAD.Package.Options;
using VSRAD.Package.ProjectSystem.Macros;
using VSRAD.Package.Utils;

namespace VSRAD.Package.ProjectSystem.Profiles
{
    [ValueConversion(typeof(IActionStep), typeof(string))]
    public sealed class StepDescriptionConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
            value.ToString();

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotImplementedException();
    }

    public partial class ActionEditor : UserControl, INotifyPropertyChanged
    {
#pragma warning disable CA2227 // WPF collection bindings need a setter
        public ObservableCollection<IActionStep> Steps
        {
            get => (ObservableCollection<IActionStep>)GetValue(StepsProperty); set => SetValue(StepsProperty, value);
        }
        public IEnumerable<ActionProfileOptions> RunActionCandidates
        {
            get => ((IEnumerable<ActionProfileOptions>)GetValue(RunActionCandidatesProperty)); set => SetValue(RunActionCandidatesProperty, value);
        }
#pragma warning restore CA2227
        public string ActionName
        {
            get => (string)GetValue(ActionNameProperty);
            set { SetValue(ActionNameProperty, value); PropertyChanged(this, new PropertyChangedEventArgs(nameof(RunActionNames))); }
        }
        public DirtyProfileMacroEditor MacroEditor
        {
            get => (DirtyProfileMacroEditor)GetValue(MacroEditorProperty); set => SetValue(MacroEditorProperty, value);
        }

        public IEnumerable<string> RunActionNames =>
            RunActionCandidates.Select(a => a.Name).Where(n => n != ActionName);

        public static readonly DependencyProperty StepsProperty =
            DependencyProperty.Register(nameof(Steps), typeof(ObservableCollection<IActionStep>), typeof(ActionEditor), new PropertyMetadata(null));
        public static readonly DependencyProperty RunActionCandidatesProperty =
            DependencyProperty.Register(nameof(RunActionCandidates), typeof(IEnumerable<ActionProfileOptions>), typeof(ActionEditor), new PropertyMetadata(null));
        public static readonly DependencyProperty ActionNameProperty =
            DependencyProperty.Register(nameof(ActionName), typeof(string), typeof(ActionEditor), new PropertyMetadata(null));
        public static readonly DependencyProperty MacroEditorProperty =
            DependencyProperty.Register(nameof(MacroEditor), typeof(DirtyProfileMacroEditor), typeof(ActionEditor), new PropertyMetadata(null));

        public event PropertyChangedEventHandler PropertyChanged;

        private IActionStep _selectedStep;
        public IActionStep SelectedStep
        {
            get => _selectedStep;
            set
            {
                _selectedStep = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedStep)));
            }
        }

        public ICommand AddCommand { get; }
        public ICommand MoveUpCommand { get; }
        public ICommand MoveDownCommand { get; }
        public ICommand DeleteCommand { get; }
        public ICommand RichEditCommand { get; }

        public ActionEditor()
        {
            AddCommand = new WpfDelegateCommand(AddStep);
            MoveUpCommand = new WpfDelegateCommand((i) => MoveStep(i, moveUp: true));
            MoveDownCommand = new WpfDelegateCommand((i) => MoveStep(i, moveUp: false));
            DeleteCommand = new WpfDelegateCommand(DeleteStep);
            RichEditCommand = new WpfDelegateCommand(OpenMacroEditor);

            InitializeComponent();
            Root.DataContext = this;
        }

        private void AddStep(object stepKind)
        {
            var step = (IActionStep)Activator.CreateInstance((Type)stepKind);
            Steps.Add(step);
            NewStepPopup.IsOpen = false;
        }

        private void MoveStep(object item, bool moveUp)
        {
            var index = Steps.IndexOf((IActionStep)item);
            Debug.Assert(index >= 0);
            if (moveUp && index > 0)
                Steps.Move(index, index - 1);
            else if (!moveUp && index != Steps.Count - 1)
                Steps.Move(index, index + 1);
        }

        private void DeleteStep(object item)
        {
            Debug.Assert(item != null);
            Steps.Remove((IActionStep)item);
        }

        private void OpenMacroEditor(object sender)
        {
            var editButton = (Button)sender;
            var action = editButton.DataContext;
            var propertyName = (string)editButton.Tag;

            VSPackage.TaskFactory.RunAsyncWithErrorHandling(() =>
                MacroEditor.EditObjectPropertyAsync(action, propertyName));
        }

        private void OpenNewStepPopup(object sender, RoutedEventArgs e) =>
            NewStepPopup.IsOpen = true;
    }
}
