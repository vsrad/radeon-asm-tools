using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using VSRAD.Package.Options;
using VSRAD.Package.Utils;

namespace VSRAD.Package.ProjectSystem.Profiles
{
    public sealed class ActionEditorDesignTimeCollection : ObservableCollection<IActionStep>
    {
        public ActionEditorDesignTimeCollection()
        {
            Add(new CopyFileStep { Direction = FileCopyDirection.RemoteToLocal, LocalPath = @"C:\Local\Path", RemotePath = "/remote/path", CheckTimestamp = true });
            Add(new ExecuteStep { Environment = StepEnvironment.Remote, Executable = "exe", Arguments = "--args", WorkingDirectory = "/workdir" });
        }
    }

    [ValueConversion(typeof(IEnumerable<ActionProfileOptions>), typeof(IEnumerable<string>))]
    public sealed class CustomActionNameConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
            ((IEnumerable<ActionProfileOptions>)value)?.Select(a => a.Name);

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotImplementedException();
    }

    [ValueConversion(typeof(IActionStep), typeof(string))]
    public sealed class StepDescriptionConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
            value.ToString();

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotImplementedException();
    }

    public partial class ActionEditor : UserControl
    {
#pragma warning disable CA2227 // WPF collection bindings need a setter
        public ObservableCollection<IActionStep> Steps
        {
            get => (ObservableCollection<IActionStep>)GetValue(StepsProperty); set => SetValue(StepsProperty, value);
        }
        public IReadOnlyList<ActionProfileOptions> CustomActions
        {
            get => (IReadOnlyList<ActionProfileOptions>)GetValue(CustomActionsProperty); set => SetValue(CustomActionsProperty, value);
        }
#pragma warning restore CA2227

        public static readonly DependencyProperty StepsProperty =
            DependencyProperty.Register(nameof(Steps), typeof(ObservableCollection<IActionStep>), typeof(ActionEditor), new PropertyMetadata(null));
        public static readonly DependencyProperty CustomActionsProperty =
            DependencyProperty.Register(nameof(CustomActions), typeof(IReadOnlyList<ActionProfileOptions>), typeof(ActionEditor), new PropertyMetadata(null));

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
            var property = action.GetType().GetProperty(propertyName);

            // TODO: invoke macro editor
            var newValue = "(Edited " + (string)property.GetValue(action) + ")";
            property.SetValue(action, newValue);
        }

        private void OpenNewStepPopup(object sender, RoutedEventArgs e) =>
            NewStepPopup.IsOpen = true;
    }
}
