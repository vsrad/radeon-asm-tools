using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using VSRAD.Package.Options;
using VSRAD.Package.ProjectSystem.Macros;
using VSRAD.Package.Utils;

namespace VSRAD.Package.ProjectSystem.Profiles
{
    public sealed class ActionEditorStepDescriptionConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
            new ActionEditorStepDescription((TextBlock)value);

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotImplementedException();
    }

    public sealed class ActionEditorStepDescription : DefaultNotifyPropertyChanged
    {
        private string _description;
        public string Description { get => _description; set => SetField(ref _description, value); }

        private readonly ActionEditor _editor;
        private readonly IActionStep _step;

        public ActionEditorStepDescription(TextBlock descriptionBlock)
        {
            _editor = (ActionEditor)descriptionBlock.Tag;
            _step = (IActionStep)descriptionBlock.DataContext;

            Description = descriptionBlock.Text;
            UpdateDescriptionInBackground();

             _step.PropertyChanged += StepPropertyChanged;
            _editor.Unloaded += EditorUnloaded;
        }

        private void EditorUnloaded(object sender, RoutedEventArgs e)
        {
            _step.PropertyChanged -= StepPropertyChanged;
            _editor.Unloaded -= EditorUnloaded;
        }

        private void StepPropertyChanged(object sender, PropertyChangedEventArgs e) =>
            UpdateDescriptionInBackground();

        private void UpdateDescriptionInBackground() =>
            VSPackage.TaskFactory.RunAsyncWithErrorHandling(() => EvaluateDescriptionAsync());

        private async Task EvaluateDescriptionAsync()
        {
            await VSPackage.TaskFactory.SwitchToMainThreadAsync();
            var evalResult = await _editor.MacroEditor.EvaluateStepAsync(_step, _editor.ActionName);
            if (evalResult.TryGetResult(out var evaluated, out var error))
                Description = evaluated.ToString();
            else
                Description = $"{_step} ({error.Message})";
        }
    }

    public partial class ActionEditor : UserControl, INotifyPropertyChanged
    {
#pragma warning disable CA2227 // WPF collection bindings need a setter
        public ObservableCollection<IActionStep> Steps
        {
            get => (ObservableCollection<IActionStep>)GetValue(StepsProperty); set => SetValue(StepsProperty, value);
        }
        public IEnumerable<string> AllActionNames
        {
            get => ((IEnumerable<string>)GetValue(AllActionNamesProperty)); set => SetValue(AllActionNamesProperty, value);
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

        public IEnumerable<string> RunActionNames => AllActionNames.Where(n => n != ActionName);

        public static readonly DependencyProperty StepsProperty =
            DependencyProperty.Register(nameof(Steps), typeof(ObservableCollection<IActionStep>), typeof(ActionEditor), new PropertyMetadata(null));
        public static readonly DependencyProperty AllActionNamesProperty =
            DependencyProperty.Register(nameof(AllActionNames), typeof(IEnumerable<string>), typeof(ActionEditor), new PropertyMetadata(null));
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
