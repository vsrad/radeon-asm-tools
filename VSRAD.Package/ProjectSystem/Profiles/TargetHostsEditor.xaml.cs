﻿using Microsoft.VisualStudio.PlatformUI;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using VSRAD.Package.Utils;

namespace VSRAD.Package.ProjectSystem.Profiles
{
    public sealed partial class TargetHostsEditor : DialogWindow
    {
        public ObservableCollection<HostItem> Hosts { get; }
        public ICommand DeleteHostCommand { get; }

        private readonly IProject _project;
        private bool _promptUnsavedOnClose = true;

        public TargetHostsEditor(IProject project)
        {
            _project = project;
            Hosts = new ObservableCollection<HostItem>(project.Options.TargetHosts);
            DeleteHostCommand = new WpfDelegateCommand(DeleteHost);

            InitializeComponent();
        }

        private void AddHost(object sender, RoutedEventArgs e)
        {
            var item = new HostItem("", 9339); // TODO: move 9339 to Constants.cs
            Hosts.Add(item);

            // Finish editing the current host before moving the focus away from it
            HostGrid.CommitEdit();

            HostGrid.SelectedItem = item;
            HostGrid.CurrentCell = new DataGridCellInfo(item, HostGrid.Columns[0]);
#pragma warning disable VSTHRD001 // Using BeginInvoke to focus on the added host item _after_ it's been added to the DataGrid
            Dispatcher.BeginInvoke((Action)(() => HostGrid.BeginEdit()), System.Windows.Threading.DispatcherPriority.Background);
#pragma warning restore VSTHRD001
        }

        private void DeleteHost(object param)
        {
            var item = (HostItem)param;
            var prompt = MessageBox.Show($"Are you sure you want to delete {item.Host}?", "Confirm host deletion", MessageBoxButton.YesNo);
            if (prompt == MessageBoxResult.Yes)
                Hosts.Remove(item);
        }

        private void SaveChanges()
        {
            var oldHost = _project.Options.TargetHosts.Count != 0
                                           ? _project.Options.TargetHosts[0]
                                           : null;
            _project.Options.TargetHosts.Clear();
            _project.Options.TargetHosts.AddRange(Hosts.Distinct());

            var updatedProfile = (Options.ProfileOptions)_project.Options.Profile.Clone();
            if (oldHost == null || !Hosts.Contains(oldHost))
                updatedProfile.General.RunActionsLocally = true;
            _project.Options.UpdateActiveProfile(updatedProfile);

            _project.SaveOptions();
        }

        private void HandleDeleteKey(object sender, KeyEventArgs e)
        {
            IEditableCollectionView itemsView = HostGrid.Items;
            if (e.Key == Key.Delete && !itemsView.IsAddingNew && !itemsView.IsEditingItem && HostGrid.CurrentItem is HostItem item)
            {
                DeleteHost(item);
                e.Handled = true;
            }
        }

        private void HandleOK(object sender, RoutedEventArgs e)
        {
            _promptUnsavedOnClose = false;
            SaveChanges();
            Close();
        }

        private void HandleCancel(object sender, RoutedEventArgs e)
        {
            _promptUnsavedOnClose = false;
            Close();
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            if (_promptUnsavedOnClose && HasChanges())
            {
                var result = MessageBox.Show($"Save changes to hosts?", "Target Hosts Editor", MessageBoxButton.YesNoCancel);
                if (result == MessageBoxResult.Yes)
                {
                    SaveChanges();
                }
                else if (result == MessageBoxResult.Cancel)
                {
                    e.Cancel = true;
                    return;
                }
            }
            base.OnClosing(e);
        }

        private bool HasChanges()
        {
            if (Hosts.Count != _project.Options.TargetHosts.Count)
                return true;

            for (int i = 0; i < Hosts.Count; ++i)
            {
                if (Hosts[i] != _project.Options.TargetHosts[i])
                    return true;
            }

            return false;
        }
    }
}
