using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Windows;
using VSRAD.Package.Options;

namespace VSRAD.Package.ProjectSystem.Profiles
{
    public partial class ProfileOptionsWindow : Window
    {
        private readonly ProfileOptionsWindowContext _context;

        public ProfileOptionsWindow(IToolWindowIntegration integration)
        {
            if (!integration.ProjectOptions.HasProfiles)
                integration.ProjectOptions.AddProfile("Default", new ProfileOptions());

            _context = new ProfileOptionsWindowContext(integration.Project, integration.CommunicationChannel, askProfileName: AskProfileName);
            DataContext = _context;
            InitializeComponent();
        }

        private void CreateNewProfile(object sender, RoutedEventArgs e)
            => _context.CreateNewProfile();

        private void CopyProfile(object sender, RoutedEventArgs e)
            => _context.CopyActiveProfile();

        private void ApplyChanges(object sender, RoutedEventArgs e) =>
            _context.SaveChanges();

        private void ApplyChangesAndClose(object sender, RoutedEventArgs e)
        {
            _context.SaveChanges();
            Close(sender, e);
        }

        private void Close(object sender, RoutedEventArgs e)
        {
            // TODO: warn if there are unsaved changes
            Close();
        }

        private void ImportProfiles(object sender, RoutedEventArgs e)
        {
            var ofd = new OpenFileDialog { Filter = "Exported profiles(*.json) | *.json" };
            if (ofd.ShowDialog() == true && !string.IsNullOrEmpty(ofd.FileName))
            {
                try
                {
                    _context.ImportProfiles(ofd.FileName);
                }
                catch (Exception)
                {
                    MessageBox.Show("Unable to read profiles from selected file", "Import failed", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }

        private void ExportProfiles(object sender, RoutedEventArgs e)
        {
            var sfd = new SaveFileDialog { Filter = "Exported profiles(*.json) | *.json" };
            if (sfd.ShowDialog() == true && !string.IsNullOrEmpty(sfd.FileName))
            {
                try
                {
                    _context.ExportProfiles(sfd.FileName);
                }
                catch (Exception)
                {
                    MessageBox.Show("Unable to export profiles to selected file", "Export failed", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }

        private static string AskProfileName(string dialogTitle, string dialogLabel, IEnumerable<string> existingNames, string initialName)
        {
            var dialog = new ProfileNameWindow(dialogLabel, okButton: "OK", cancelButton: "Cancel", initialName, existingNames)
            {
                Title = dialogTitle
            };
            dialog.ShowDialog();
            return dialog.DialogResult == true ? dialog.EnteredName : null;
        }
    }
}
