using Microsoft.VisualStudio.PlatformUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace VSRAD.Package.Utils
{
    public partial class WpfMruEditor : DialogWindow
    {
        public delegate object CreateItemDelegate();
        public delegate bool ValidateEditedItemDelegate(object item);
        public delegate bool CheckHaveUnsavedChangesDelegate(IReadOnlyList<object> items);
        public delegate void SaveChangesDelegate(IReadOnlyList<object> items);

        public CreateItemDelegate CreateItem { get; set; }
        public ValidateEditedItemDelegate ValidateEditedItem { get; set; }
        public CheckHaveUnsavedChangesDelegate CheckHaveUnsavedChanges { get; set; }
        public SaveChangesDelegate SaveChanges { get; set; }

        public ObservableCollection<object> Items { get; }
        public ICommand RemoveItemCommand { get; }

        private bool _promptUnsavedOnClose = true;

        public WpfMruEditor() : this("", "", Array.Empty<object>())
        {
        }

        public WpfMruEditor(string itemHeader, string helpMessage, IEnumerable<object> items)
        {
            Items = new ObservableCollection<object>(items);
            RemoveItemCommand = new WpfDelegateCommand(RemoveItem);

            InitializeComponent();

            ItemGrid.Columns[0].Header = itemHeader;
            HelpMessage.Text = helpMessage;
            Title = $"{itemHeader} Editor";
            ShowInTaskbar = false;
        }

        private void AddItem(object sender, RoutedEventArgs e)
        {
            var item = CreateItem();
            Items.Add(item);

            // Finish editing the current host before moving the focus away from it
            ItemGrid.CommitEdit();

            ItemGrid.SelectedItem = item;
            ItemGrid.CurrentCell = new DataGridCellInfo(item, ItemGrid.Columns[0]);
#pragma warning disable VSTHRD001 // Using BeginInvoke to focus on the added host item _after_ it's been added to the DataGrid
            Dispatcher.BeginInvoke((Action)(() => ItemGrid.BeginEdit()), System.Windows.Threading.DispatcherPriority.Background);
#pragma warning restore VSTHRD001
        }

        private void RemoveItem(object item)
        {
            var prompt = MessageBox.Show($"Are you sure you want to remove {((dynamic)item).FormattedValue}?", "Confirm removal", MessageBoxButton.YesNo);
            if (prompt == MessageBoxResult.Yes)
                Items.Remove(item);
        }

        private void ValidateItemAfterEdit(object sender, DataGridRowEditEndingEventArgs e)
        {
            var item = e.Row.DataContext;
            if (!ValidateEditedItem(item))
#pragma warning disable VSTHRD001 // Using BeginInvoke to remove the item after all post-edit events fire
                Dispatcher.BeginInvoke((Action)(() => Items.Remove(item)), System.Windows.Threading.DispatcherPriority.Background);
#pragma warning restore VSTHRD001
        }

        private void HandleDeleteKey(object sender, KeyEventArgs e)
        {
            IEditableCollectionView itemsView = ItemGrid.Items;
            if (e.Key == Key.Delete && !itemsView.IsAddingNew && !itemsView.IsEditingItem && ItemGrid.CurrentItem is object item)
            {
                RemoveItem(item);
                e.Handled = true;
            }
        }

        private void HandleOK(object sender, RoutedEventArgs e)
        {
            _promptUnsavedOnClose = false;
            SaveChanges?.Invoke(Items);
            Close();
        }

        private void HandleCancel(object sender, RoutedEventArgs e)
        {
            _promptUnsavedOnClose = false;
            Close();
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            if (_promptUnsavedOnClose && (CheckHaveUnsavedChanges?.Invoke(Items) ?? false))
            {
                var result = MessageBox.Show($"Save changes?", "Close editor", MessageBoxButton.YesNoCancel);
                if (result == MessageBoxResult.Yes)
                {
                    SaveChanges?.Invoke(Items);
                }
                else if (result == MessageBoxResult.Cancel)
                {
                    e.Cancel = true;
                    return;
                }
            }
            base.OnClosing(e);
        }
    }
}