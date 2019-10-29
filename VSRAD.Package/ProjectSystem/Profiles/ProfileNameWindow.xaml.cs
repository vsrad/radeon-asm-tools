using System.Collections.Generic;
using System.Linq;
using System.Windows;
using VSRAD.Package.Utils;

namespace VSRAD.Package.ProjectSystem.Profiles
{
    public delegate IEnumerable<string> GetExistingProfileNamesDelegate();

    public partial class ProfileNameWindow : Window
    {
        private sealed class Context : DefaultNotifyPropertyChanged
        {
            public string OkButton { get; set; }
            public string CancelButton { get; set; }

            private string _enteredName;
            private string _previouslyValidatedName;
            public string EnteredName { get => _enteredName; set => SetField(ref _enteredName, value); }

            private string _message;
            public string Message { get => _message; set => SetField(ref _message, value); }

            public GetExistingProfileNamesDelegate GetExistingNames { get; set; }

            public bool ValidateName()
            {
                if (_previouslyValidatedName == EnteredName)
                    return true; // the same name entered twice => overwrite
                if (!GetExistingNames().Contains(EnteredName))
                    return true;

                _previouslyValidatedName = EnteredName;
                Message = NameConflictMessage(EnteredName);
                return false;
            }
        }

        public string EnteredName => ((Context)DataContext).EnteredName;

        public static string NameConflictMessage(string name) => $"Profile {name} already exists. Enter a new name or leave it as is to overwrite the profile:";

        public ProfileNameWindow(string message, string okButton, string cancelButton, string initialValue, GetExistingProfileNamesDelegate getExistingNames)
        {
            DataContext = new Context { Message = message, OkButton = okButton, CancelButton = cancelButton, EnteredName = initialValue, GetExistingNames = getExistingNames };
            InitializeComponent();
        }

        private void OkClicked(object sender, RoutedEventArgs e)
        {
            if (!((Context)DataContext).ValidateName()) return;

            DialogResult = true;
            Close();
        }
    }
}
