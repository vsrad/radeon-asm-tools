using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
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
            Add(new ExecuteStep { Environment = StepEnvironment.Remote, Executable = "exe", Arguments = "--args" });
        }
    }

    [ValueConversion(typeof(IActionStep), typeof(string))]
    public class StepDescriptionConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
            value.ToString();

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotImplementedException();
    }

    public partial class ActionEditor : UserControl
    {
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
        }

        private void AddStep(object stepKind)
        {
            var step = (IActionStep)Activator.CreateInstance((Type)stepKind);
            ((ObservableCollection<IActionStep>)DataContext).Add(step);
            NewStepPopup.IsOpen = false;
        }

        private void MoveStep(object item, bool moveUp)
        {
            var sequence = ((ObservableCollection<IActionStep>)DataContext);
            var index = sequence.IndexOf((IActionStep)item);
            Debug.Assert(index >= 0);
            if (moveUp && index > 0)
                sequence.Move(index, index - 1);
            else if (!moveUp && index != sequence.Count - 1)
                sequence.Move(index, index + 1);
        }

        private void DeleteStep(object item)
        {
            Debug.Assert(item != null);
            ((ObservableCollection<IActionStep>)DataContext).Remove((IActionStep)item);
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

        private void OpenNewStepPopup(object sender, System.Windows.RoutedEventArgs e) =>
            NewStepPopup.IsOpen = true;
    }
}
