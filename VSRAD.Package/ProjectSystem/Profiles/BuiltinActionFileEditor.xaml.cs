using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using VSRAD.Package.Utils;

namespace VSRAD.Package.ProjectSystem.Profiles
{
    public partial class BuiltinActionFileEditor : UserControl
    {
        public static readonly DependencyProperty HeaderProperty =
            DependencyProperty.Register(nameof(Header), typeof(string), typeof(BuiltinActionFileEditor), new PropertyMetadata("Action File"));

        public string Header
        {
            get => (string)GetValue(HeaderProperty);
            set => SetValue(HeaderProperty, value);
        }

        public ICommand RichEditCommand { get; }

        public BuiltinActionFileEditor()
        {
            RichEditCommand = new WpfDelegateCommand((_) => throw new NotImplementedException());
            InitializeComponent();
        }
    }
}
