using System.Windows;
using System.Windows.Controls;

namespace VSRAD.Package.ProjectSystem.Profiles
{
    public partial class ExtendedTreeView : TreeView
    {
        public object BindableSelectedItem
        {
            get => GetValue(BindableSelectedItemProperty);
            set => SetValue(BindableSelectedItemProperty, value);
        }

        public static readonly DependencyProperty BindableSelectedItemProperty =
            DependencyProperty.Register(nameof(BindableSelectedItem), typeof(object), typeof(ExtendedTreeView), new PropertyMetadata(null));

        public ExtendedTreeView()
        {
            InitializeComponent();
            SelectedItemChanged += UpdateBindableSelectedItem;
        }

        private void UpdateBindableSelectedItem(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            BindableSelectedItem = SelectedItem;
        }
    }
}
