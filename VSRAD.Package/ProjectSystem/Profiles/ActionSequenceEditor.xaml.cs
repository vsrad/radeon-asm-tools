using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows.Controls;
using System.Windows.Input;
using VSRAD.Package.Options;
using VSRAD.Package.Utils;

namespace VSRAD.Package.ProjectSystem.Profiles
{
    public sealed class ActionSequenceEditorDesignTimeCollection : ObservableCollection<IAction>
    {
        public ActionSequenceEditorDesignTimeCollection()
        {
            Add(new CopyFileAction { Direction = FileCopyDirection.RemoteToLocal, LocalPath = @"C:\Local\Path", RemotePath = "/remote/path", CheckTimestamp = true });
            Add(new ExecuteAction { Environment = ActionEnvironment.Remote, Executable = "exe", Arguments = "--args" });
        }
    }

    public partial class ActionSequenceEditor : UserControl
    {
        public ICommand AddCommand { get; }
        public ICommand MoveUpCommand { get; }
        public ICommand MoveDownCommand { get; }
        public ICommand DeleteCommand { get; }
        public ICommand RichEditCommand { get; }

        public ActionSequenceEditor()
        {
            AddCommand = new WpfDelegateCommand(AddAction);
            MoveUpCommand = new WpfDelegateCommand((i) => MoveAction(i, moveUp: true));
            MoveDownCommand = new WpfDelegateCommand((i) => MoveAction(i, moveUp: false));
            DeleteCommand = new WpfDelegateCommand(DeleteAction);
            RichEditCommand = new WpfDelegateCommand(OpenMacroEditor);
            InitializeComponent();
        }

        private void AddAction(object actionKind)
        {
            var action = (IAction)Activator.CreateInstance((Type)actionKind);
            ((ObservableCollection<IAction>)DataContext).Add(action);
            NewActionPopup.IsOpen = false;
        }

        private void MoveAction(object item, bool moveUp)
        {
            var sequence = ((ObservableCollection<IAction>)DataContext);
            var index = sequence.IndexOf((IAction)item);
            Debug.Assert(index >= 0);
            if (moveUp && index > 0)
                sequence.Move(index, index - 1);
            else if (!moveUp && index != sequence.Count - 1)
                sequence.Move(index, index + 1);
        }

        private void DeleteAction(object item)
        {
            Debug.Assert(item != null);
            ((ObservableCollection<IAction>)DataContext).Remove((IAction)item);
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

        private void OpenNewActionPopup(object sender, System.Windows.RoutedEventArgs e) =>
            NewActionPopup.IsOpen = true;
    }
}
