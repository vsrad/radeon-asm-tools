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
            foreach (var action in profile.General.Actions)
                Actions.Add(action);

            profile.General.Actions.CollectionChanged += (s, e) =>
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

        public ProjectOptions Options { get; }

        public IEnumerable<ActionProfileOptions> Actions => _dirtyOptions[Options.ActiveProfile].General.Actions;

        public List<object> Pages { get; } = new List<object>();

        private object _selectedPage;
        public object SelectedPage { get => _selectedPage; set => SetField(ref _selectedPage, value); }

        public IReadOnlyList<string> ProfileNames => Options.Profiles.Keys.ToList();

        public WpfDelegateCommand AddActionCommand { get; }
        public WpfDelegateCommand RemoveActionCommand { get; }
        public WpfDelegateCommand RemoveProfileCommand { get; }
        public WpfDelegateCommand RichEditCommand { get; }

        public DirtyProfileMacroEditor MacroEditor { get; private set; }

        private readonly Dictionary<string, ProfileOptions> _dirtyOptions = new Dictionary<string, ProfileOptions>();
        private readonly AskProfileNameDelegate _askProfileName;
        private readonly IProject _project;
        private readonly ICommunicationChannel _channel;

        public ProfileOptionsWindowContext(IProject project, ICommunicationChannel channel, AskProfileNameDelegate askProfileName)
        {
            Options = project.Options;
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
            _askProfileName = askProfileName;
            _project = project;
            _channel = channel;
            AddActionCommand = new WpfDelegateCommand(AddAction);
            RemoveActionCommand = new WpfDelegateCommand(RemoveAction);
            RemoveProfileCommand = new WpfDelegateCommand(RemoveProfile, isEnabled: ProfileNames.Count > 1);
            RichEditCommand = new WpfDelegateCommand(OpenMacroEditor);
            OpenActiveProfilePages();
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
            var currentProfile = _dirtyOptions[Options.ActiveProfile];
            MacroEditor = new DirtyProfileMacroEditor(_project, _channel, currentProfile);
            Pages.Clear();
            Pages.Add(currentProfile.General);
            Pages.Add(new ProfileOptionsMacrosPage(currentProfile.General.Macros));
            Pages.Add(new ProfileOptionsActionsPage(currentProfile));
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

        private void AddAction(object param) =>
            _dirtyOptions[Options.ActiveProfile].General.Actions.Add(new ActionProfileOptions { Name = "New Action" });

        private void RemoveAction(object param) =>
            _dirtyOptions[Options.ActiveProfile].General.Actions.Remove((ActionProfileOptions)param);

        private void RemoveProfile(object param)
        {
            _dirtyOptions.Remove(Options.ActiveProfile);
            Options.RemoveProfile(Options.ActiveProfile);
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
