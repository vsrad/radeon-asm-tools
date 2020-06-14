using System.Collections.Generic;
using System.Linq;
using VSRAD.Package.Options;
using VSRAD.Package.Utils;

namespace VSRAD.Package.ProjectSystem.Profiles
{
    public sealed class ProfileOptionsWindowContext : DefaultNotifyPropertyChanged
    {
        public delegate string AskProfileNameDelegate(string title, string message, IEnumerable<string> existingNames, string initialName);

        public ProjectOptions Options { get; }

        public List<object> Pages { get; } = new List<object>();

        private object _selectedPage;
        public object SelectedPage { get => _selectedPage; set => SetField(ref _selectedPage, value); }

        public IReadOnlyList<string> ProfileNames => Options.Profiles.Keys.ToList();

        public WpfDelegateCommand RemoveProfileCommand { get; }

        private readonly Dictionary<string, ProfileOptions> _dirtyOptions = new Dictionary<string, ProfileOptions>();
        private readonly AskProfileNameDelegate _askProfileName;

        public ProfileOptionsWindowContext(ProjectOptions projectOptions, AskProfileNameDelegate askProfileName)
        {
            Options = projectOptions;
            Options.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(Options.ActiveProfile))
                    OpenActiveProfilePages();
            };
            Options.Profiles.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName != nameof(Options.Profiles.Keys))
                    return;
                RaisePropertyChanged(nameof(ProfileNames));
                RemoveProfileCommand.IsEnabled = ProfileNames.Count > 1;
            };
            OpenActiveProfilePages();
            _askProfileName = askProfileName;
            RemoveProfileCommand = new WpfDelegateCommand(RemoveProfile, isEnabled: ProfileNames.Count > 1);
        }

        public void CreateNewProfile() =>
            AddProfile("Creating a new profile", "Enter the name for the new profile:", new ProfileOptions());

        public void CopyActiveProfile() =>
            AddProfile("Copy profile", "Enter the name for the new profile:", (ProfileOptions)Options.Profile.Clone());

        public void ImportProfiles(string file)
        {
            string ResolveNameConflict(string name) =>
                _askProfileName(title: "Import", message: ProfileNameWindow.NameConflictMessage(name), existingNames: ProfileNames, initialName: name);
            new ProfileTransferManager(Options, ResolveNameConflict).Import(file);
        }

        public void ExportProfiles(string file) =>
            new ProfileTransferManager(Options, null).Export(file);

        public void SaveChanges()
        {
            string ResolveNameConflict(string name) =>
                _askProfileName(title: "Rename", message: ProfileNameWindow.NameConflictMessage(name), existingNames: ProfileNames, initialName: name);
            Options.UpdateProfiles(_dirtyOptions, ResolveNameConflict);
            _dirtyOptions.Clear();
            OpenActiveProfilePages();
        }

        private void OpenActiveProfilePages()
        {
            if (!_dirtyOptions.ContainsKey(Options.ActiveProfile))
                _dirtyOptions.Add(Options.ActiveProfile, (ProfileOptions)Options.Profiles[Options.ActiveProfile].Clone());
            var currentPages = _dirtyOptions[Options.ActiveProfile];
            Pages.Clear();
            Pages.Add(currentPages.General);
            Pages.Add(currentPages.Debugger);
            RaisePropertyChanged(nameof(Pages));
        }

        private void AddProfile(string title, string message, ProfileOptions profile)
        {
            var name = _askProfileName(title, message, ProfileNames, "");
            if (!string.IsNullOrWhiteSpace(name))
            {
                _dirtyOptions[name] = profile;
                SaveChanges();
                Options.ActiveProfile = name;
            }
        }

        private void RemoveProfile(object sender)
        {
            _dirtyOptions.Remove(Options.ActiveProfile);
            Options.RemoveProfile(Options.ActiveProfile);
        }
    }
}
