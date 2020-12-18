using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using VSRAD.Package.Options;
using VSRAD.Package.ProjectSystem.Macros;
using VSRAD.Package.Utils;

namespace VSRAD.Package.ProjectSystem.Profiles
{
    public partial class BuiltinActionFileEditor : UserControl
    {
        public string Header
        {
            get => (string)GetValue(HeaderProperty);
            set => SetValue(HeaderProperty, value);
        }
        public DirtyProfileMacroEditor MacroEditor
        {
            get => (DirtyProfileMacroEditor)GetValue(MacroEditorProperty); set => SetValue(MacroEditorProperty, value);
        }
        public ProfileOptions CurrentProfile
        {
            get => ((ProfileOptions)GetValue(CurrentProfileProperty)); set => SetValue(CurrentProfileProperty, value);
        }

        public static readonly DependencyProperty HeaderProperty =
            DependencyProperty.Register(nameof(Header), typeof(string), typeof(BuiltinActionFileEditor), new PropertyMetadata("Action File"));
        public static readonly DependencyProperty MacroEditorProperty =
            DependencyProperty.Register(nameof(MacroEditor), typeof(DirtyProfileMacroEditor), typeof(BuiltinActionFileEditor), new PropertyMetadata(null));
        public static readonly DependencyProperty CurrentProfileProperty =
            DependencyProperty.Register(nameof(CurrentProfile), typeof(ProfileOptions), typeof(BuiltinActionFileEditor), new PropertyMetadata(null));

        public ICommand RichEditCommand { get; }

        public BuiltinActionFileEditor()
        {
            RichEditCommand = new WpfDelegateCommand(OpenMacroEditor);
            InitializeComponent();
        }

        private void OpenMacroEditor(object sender)
        {
            var editButton = (Button)sender;
            var file = editButton.DataContext;
            var propertyName = (string)editButton.Tag;

            VSPackage.TaskFactory.RunAsyncWithErrorHandling(() =>
                MacroEditor.EditObjectPropertyAsync(file, propertyName));
        }
    }
}
