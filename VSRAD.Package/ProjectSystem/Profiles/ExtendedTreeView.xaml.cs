using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

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
            DependencyProperty.Register(nameof(BindableSelectedItem), typeof(object), typeof(ExtendedTreeView), new PropertyMetadata(null, SelectedItemBindingChanged));

        public ExtendedTreeView()
        {
            InitializeComponent();
            SelectedItemChanged += UpdateBindableSelectedItem;
        }

        private void UpdateBindableSelectedItem(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            BindableSelectedItem = SelectedItem;
        }

        private static void SelectedItemBindingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (e.NewValue == null)
                return;

            ((TreeView)d).UpdateLayout();
            SelectItem(((TreeView)d).ItemContainerGenerator, e.NewValue);
        }

        private static void SelectItem(ItemContainerGenerator generator, object item)
        {
            if (generator.ContainerFromItem(item) is TreeViewItem container)
            {
                container.IsSelected = true;
                return;
            }

            foreach (object child in generator.Items)
            {
                if (generator.ContainerFromItem(child) is TreeViewItem parent && parent.Items.Contains(item))
                {
                    parent.IsExpanded = true;
                    generator = parent.ItemContainerGenerator;
                    break;
                }
            }

            if (generator.Status == GeneratorStatus.NotStarted)
                generator.StatusChanged += (s, e) =>
                {
                    if (generator.ContainerFromItem(item) is TreeViewItem childContainer)
                        childContainer.IsSelected = true;
                };
            else if (generator.ContainerFromItem(item) is TreeViewItem childContainer)
                childContainer.IsSelected = true;
        }
    }
}
