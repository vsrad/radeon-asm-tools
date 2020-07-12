using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Windows.Controls;
using VSRAD.Package.Options;
using VSRAD.Package.ProjectSystem.Macros;
using VSRAD.Package.Server;
using VSRAD.Package.Utils;

namespace VSRAD.Package.ProjectSystem.Profiles
{
    public sealed class ProfileOptionsActionsPage
    {
        public ObservableCollection<object> Actions { get; }

        public ProfileOptionsActionsPage(ProfileOptions profile)
        {
            Actions = new ObservableCollection<object> { profile.Debugger };
            foreach (var action in profile.Actions)
                Actions.Add(action);

            profile.Actions.CollectionChanged += (s, e) =>
            {
                if (e.Action == NotifyCollectionChangedAction.Remove)
                {
                    foreach (var item in e.OldItems)
                        Actions.Remove(item);
                }
                else if (e.Action == NotifyCollectionChangedAction.Add)
                {
                    foreach (var item in e.NewItems)
                        Actions.Add(item);
                }
            };
        }
    }

    public sealed class ProfileOptionsMacrosPage
    {
        public ObservableCollection<MacroItem> Macros { get; }

        public ProfileOptionsMacrosPage(ObservableCollection<MacroItem> macros)
        {
            Macros = macros;
        }
    }

    public sealed class ProfileOptionsWindowContext : DefaultNotifyPropertyChanged
    {
        public delegate string AskProfileNameDelegate(string title, string message, IEnumerable<string> existingNames, string initialName);

        public ObservableCollection<ProfileOptions> DirtyProfiles { get; } = new ObservableCollection<ProfileOptions>();

        private ProfileOptions _selectedProfile;
        public ProfileOptions SelectedProfile
        {
            get => _selectedProfile;
            set
            {
                SetField(ref _selectedProfile, value);
                OpenSelectedProfilePages();
            }
        }

        public ObservableCollection<object> Pages { get; } = new ObservableCollection<object>();

        private object _selectedPage;
        public object SelectedPage { get => _selectedPage; set => SetField(ref _selectedPage, value); }

        public IEnumerable<ActionProfileOptions> Actions => SelectedProfile.Actions;

        // TODO: remove
        public IReadOnlyList<string> ProfileNames => DirtyProfiles.Select(p => p.General.ProfileName).ToList();

        public WpfDelegateCommand AddActionCommand { get; }
        public WpfDelegateCommand RemoveActionCommand { get; }
        public WpfDelegateCommand RemoveProfileCommand { get; }
        public WpfDelegateCommand RichEditCommand { get; }

        public DirtyProfileMacroEditor MacroEditor { get; private set; }

        private readonly AskProfileNameDelegate _askProfileName;
        private readonly IProject _project;
        private readonly ICommunicationChannel _channel;

        public ProfileOptionsWindowContext(IProject project, ICommunicationChannel channel, AskProfileNameDelegate askProfileName)
        {
            _askProfileName = askProfileName;
            _project = project;
            _channel = channel;

            PopulateDirtyProfiles();
            AddActionCommand = new WpfDelegateCommand(AddAction);
            RemoveActionCommand = new WpfDelegateCommand(RemoveAction);
            RemoveProfileCommand = new WpfDelegateCommand(RemoveProfile, isEnabled: DirtyProfiles.Count > 1);
            RichEditCommand = new WpfDelegateCommand(OpenMacroEditor);

            DirtyProfiles.CollectionChanged += (s, e) =>
            {
                RemoveProfileCommand.IsEnabled = DirtyProfiles.Count > 1;
                RaisePropertyChanged(nameof(ProfileNames));
            };
        }

        private void PopulateDirtyProfiles()
        {
            DirtyProfiles.Clear();
            foreach (var profile in _project.Options.Profiles)
            {
                var dirtyProfile = (ProfileOptions)profile.Value.Clone();
                dirtyProfile.General.ProfileName = profile.Key;
                DirtyProfiles.Add(dirtyProfile);
                if (profile.Key == _project.Options.ActiveProfile)
                    SelectedProfile = dirtyProfile;
            }
        }

        private void OpenSelectedProfilePages()
        {
            SelectedPage = null;
            if (SelectedProfile != null)
            {
                MacroEditor = new DirtyProfileMacroEditor(_project, _channel, SelectedProfile);
                Pages.Clear();
                Pages.Add(SelectedProfile.General);
                Pages.Add(new ProfileOptionsMacrosPage(SelectedProfile.Macros));
                Pages.Add(new ProfileOptionsActionsPage(SelectedProfile));
            }
        }

        public void CreateNewProfile()
        {
            var profile = new ProfileOptions();
            foreach (var (macro, value) in CleanProfileMacros.Macros)
                profile.Macros.Add(new MacroItem(macro, value, userDefined: true));
            AddProfile("Creating a new profile", "Enter the name for the new profile:", profile);
        }

        public void CopyActiveProfile()
        {
            var profile = (ProfileOptions)_project.Options.Profile.Clone();
            AddProfile("Copy profile", "Enter the name for the new profile:", profile);
        }

        public void ImportProfiles(string file)
        {
            foreach (var importedProfile in ProfileTransferManager.Import(file))
            {
                var name = importedProfile.Key;
                if (ProfileNames.Contains(name))
                {
                    name = _askProfileName(title: "Import", message: ProfileNameWindow.NameConflictMessage(name), existingNames: ProfileNames, initialName: name);
                    DirtyProfiles.RemoveAll(p => p.General.ProfileName == name);
                }
                importedProfile.Value.General.ProfileName = name;
                DirtyProfiles.Add(importedProfile.Value);
            }
        }

        public void ExportProfiles(string file)
        {
            SaveChanges();
            ProfileTransferManager.Export((IDictionary<string, ProfileOptions>)_project.Options.Profiles, file);
        }

        private void AddProfile(string title, string message, ProfileOptions profile)
        {
            var name = _askProfileName(title, message, ProfileNames, "");
            if (!string.IsNullOrWhiteSpace(name))
            {
                DirtyProfiles.RemoveAll(p => p.General.ProfileName == name);

                profile.General.ProfileName = name;
                DirtyProfiles.Add(profile);
                SelectedProfile = profile;
            }
        }

        private void AddAction(object param) =>
            SelectedProfile.Actions.Add(new ActionProfileOptions { Name = "New Action" });

        private void RemoveAction(object param) =>
            SelectedProfile.Actions.Remove((ActionProfileOptions)param);

        private void RemoveProfile(object param) =>
            DirtyProfiles.Remove(SelectedProfile);

        public void SaveChanges()
        {
            var profiles = new Dictionary<string, ProfileOptions>();
            foreach (var p in DirtyProfiles)
            {
                var name = p.General.ProfileName;
                if (profiles.ContainsKey(name))
                    name = _askProfileName(title: "Rename", message: ProfileNameWindow.NameConflictMessage(name), existingNames: ProfileNames, initialName: name);

                profiles[name] = p;
            }

            _project.Options.SetProfiles(profiles, activeProfile: SelectedProfile.General.ProfileName);
            PopulateDirtyProfiles();
        }

        private void OpenMacroEditor(object sender)
        {
            var editButton = (Button)sender;
            var options = editButton.DataContext;
            var propertyName = (string)editButton.Tag;
            VSPackage.TaskFactory.RunAsyncWithErrorHandling(() =>
                MacroEditor.EditObjectPropertyAsync(options, propertyName));
        }
    }
}
