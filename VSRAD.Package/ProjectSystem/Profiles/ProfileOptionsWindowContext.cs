using Microsoft.VisualStudio.Shell;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using VSRAD.Package.Options;
using VSRAD.Package.ProjectSystem.Macros;
using VSRAD.Package.Server;
using VSRAD.Package.Utils;

namespace VSRAD.Package.ProjectSystem.Profiles
{
    public sealed class ActionNameWithNoneCollectionConverter : IValueConverter
    {
        public object Convert(object value, Type _, object _1, CultureInfo _2) =>
            ((IEnumerable<string>)value).Prepend("(None)");

        public object ConvertBack(object value, Type _, object _1, CultureInfo _2) =>
            throw new NotImplementedException();
    }

    public sealed class ActionNameWithNoneDisplayConverter : IValueConverter
    {
        public object Convert(object value, Type _, object _1, CultureInfo _2) =>
            string.IsNullOrEmpty((string)value) ? "(None)" : value;

        public object ConvertBack(object value, Type _, object _1, CultureInfo _2) =>
            (string)value == "(None)" ? "" : value;
    }

    public sealed class NonEmptyNameValidationRule : ValidationRule
    {
        public string TargetName { get; set; }

        public override ValidationResult Validate(object value, CultureInfo cultureInfo)
        {
            var targetName = (string)value;
            if (string.IsNullOrWhiteSpace(targetName))
                return new ValidationResult(false, $"{TargetName} name cannot be empty or whitespace only");
            if (targetName == "(None)")
                return new ValidationResult(false, $"{TargetName} cannot be named \"(None)\"");

            return ValidationResult.ValidResult;
        }
    }

    public sealed class ProfileOptionsActionsPage : DefaultNotifyPropertyChanged
    {
        public ObservableCollection<ActionProfileOptions> Pages { get; }

        public IEnumerable<string> ActionNames => Pages.Select(a => a.Name);

        private readonly ProfileOptions _profile;

        public ProfileOptionsActionsPage(ProfileOptions profile)
        {
            _profile = profile;
            Pages = profile.Actions;
            SyncPagesWithActionCollection(null, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, profile.Actions));
            CollectionChangedEventManager.AddHandler(profile.Actions, SyncPagesWithActionCollection);
        }

        private void SyncPagesWithActionCollection(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Add)
            {
                foreach (ActionProfileOptions action in e.NewItems)
                    WeakEventManager<ActionProfileOptions, ActionNameChangedEventArgs>.AddHandler(
                        action, nameof(ActionProfileOptions.NameChanged), OnActionNameChanged);
            }
            else if (e.Action == NotifyCollectionChangedAction.Remove)
            {
                foreach (ActionProfileOptions action in e.OldItems)
                    WeakEventManager<ActionProfileOptions, ActionNameChangedEventArgs>.RemoveHandler(
                        action, nameof(ActionProfileOptions.NameChanged), OnActionNameChanged);
            }
            RaisePropertyChanged(nameof(ActionNames));
            if (!Pages.Any(a => a.Name == _profile.MenuCommands.DebugAction))
                _profile.MenuCommands.DebugAction = null;
            if (!Pages.Any(a => a.Name == _profile.MenuCommands.ProfileAction))
                _profile.MenuCommands.ProfileAction = null;
            if (!Pages.Any(a => a.Name == _profile.MenuCommands.DisassembleAction))
                _profile.MenuCommands.DisassembleAction = null;
            if (!Pages.Any(a => a.Name == _profile.MenuCommands.PreprocessAction))
                _profile.MenuCommands.PreprocessAction = null;
        }

        private void OnActionNameChanged(object sender, ActionNameChangedEventArgs e)
        {
            foreach (var page in Pages)
            {
                foreach (var step in page.Steps)
                    if (step is RunActionStep runAction && runAction.Name == e.OldName)
                        runAction.Name = e.NewName;
            }
            RaisePropertyChanged(nameof(ActionNames));
            if (_profile.MenuCommands.DebugAction == e.OldName)
                _profile.MenuCommands.DebugAction = e.NewName;
            if (_profile.MenuCommands.ProfileAction == e.OldName)
                _profile.MenuCommands.ProfileAction = e.NewName;
            if (_profile.MenuCommands.DisassembleAction == e.OldName)
                _profile.MenuCommands.DisassembleAction = e.NewName;
            if (_profile.MenuCommands.PreprocessAction == e.OldName)
                _profile.MenuCommands.PreprocessAction = e.NewName;
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
        public ProfileOptions SelectedProfile { get => _selectedProfile; set => SelectProfile(value); }

        public ObservableCollection<object> Pages { get; } = new ObservableCollection<object>();

        private object _selectedPage;
        public object SelectedPage { get => _selectedPage; set => SetField(ref _selectedPage, value); }

        public ProfileOptionsActionsPage ActionsPage { get; private set; }

        public IReadOnlyList<string> ProfileNames => DirtyProfiles.Select(p => p.General.ProfileName).ToList();

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

            SetupDirtyProfiles();
            RichEditCommand = new WpfDelegateCommand(OpenMacroEditor);
        }

        private void SetupDirtyProfiles()
        {
            foreach (var profile in _project.Options.Profiles)
            {
                var dirtyProfile = (ProfileOptions)profile.Value.Clone();
                dirtyProfile.General.ProfileName = profile.Key;
                DirtyProfiles.Add(dirtyProfile);
                if (profile.Key == _project.Options.ActiveProfile)
                    SelectedProfile = dirtyProfile;
            }
            DirtyProfiles.CollectionChanged += (s, e) =>
                RaisePropertyChanged(nameof(ProfileNames));
        }

        private void SelectProfile(ProfileOptions newProfile)
        {
            var oldSelectedPage = SelectedPage;
            Pages.Clear();

            if (newProfile != null)
            {
                MacroEditor = new DirtyProfileMacroEditor(_project, _channel, newProfile);
                ActionsPage = new ProfileOptionsActionsPage(newProfile);
                Pages.Add(newProfile.General);
                Pages.Add(new ProfileOptionsMacrosPage(newProfile.Macros));
                Pages.Add(newProfile.MenuCommands);
                Pages.Add(ActionsPage);
                SelectedPage = FindOldSelectedPageInNewPages(oldSelectedPage, Pages);
            }
            else
            {
                SelectedPage = null;
            }

            SetField(ref _selectedProfile, newProfile, propertyName: nameof(SelectedProfile));
        }

        private static object FindOldSelectedPageInNewPages(object oldSelectedPage, IEnumerable<object> newPages)
        {
            switch (oldSelectedPage)
            {
                case GeneralProfileOptions _:
                    return newPages.First(p => p is GeneralProfileOptions);
                case ProfileOptionsMacrosPage _:
                    return newPages.First(p => p is ProfileOptionsMacrosPage);
                case MenuCommandProfileOptions _:
                    return newPages.First(p => p is MenuCommandProfileOptions);
                case ActionProfileOptions action:
                    var actionsPage = (ProfileOptionsActionsPage)newPages.First(p => p is ProfileOptionsActionsPage);
                    return actionsPage.Pages.FirstOrDefault(p => p is ActionProfileOptions a && a.Name == action.Name);
                default:
                    return null;
            }
        }

        public void CreateNewProfile()
        {
            var profile = new ProfileOptions();
            foreach (var (macro, value) in CleanProfileMacros.Macros)
                profile.Macros.Add(new MacroItem(macro, value, userDefined: true));
            AddProfile("Creating a new profile", "Enter the name for the new profile:", profile);
        }

        public void CopySelectedProfile()
        {
            var profile = (ProfileOptions)_project.Options.Profile.Clone();
            AddProfile("Copy profile", "Enter the name for the new profile:", profile);
        }

        public void RemoveSelectedProfile() =>
            DirtyProfiles.Remove(SelectedProfile);

        public void AddAction()
        {
            var newAction = new ActionProfileOptions { Name = "New Action" };
            SelectedProfile.Actions.Add(newAction);
            SelectedPage = newAction;
        }

        public void RemoveAction(ActionProfileOptions action) =>
            SelectedProfile.Actions.Remove(action);

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

        public Error? ExportProfiles(string file)
        {
            if (SaveChanges() is Error e)
                return e;

            ProfileTransferManager.Export((IDictionary<string, ProfileOptions>)_project.Options.Profiles, file);
            return null;
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

        public Error? SaveChanges()
        {
            var profiles = new Dictionary<string, ProfileOptions>();
            for (int i = 0; i < DirtyProfiles.Count; ++i) // not using an enumerator because we may remove elements
            {
                var currProfile = DirtyProfiles[i];
                var name = currProfile.General.ProfileName;
                if (profiles.ContainsKey(name))
                {
                    var newName = _askProfileName(title: "Rename",
                        message: ProfileNameWindow.NameConflictMessage(name),
                        existingNames: ProfileNames, initialName: name);
                    if (string.IsNullOrEmpty(newName))
                        return new Error("Profile options were not saved.");
                    currProfile.General.ProfileName = newName;
                    if (newName == name)
                    {
                        var replacedProfile = DirtyProfiles.First(p => p.General.ProfileName == name);
                        DirtyProfiles.Remove(replacedProfile);
                        i--; // the index is shifted by 1 after removing the profile
                        if (SelectedProfile == replacedProfile)
                            SelectedProfile = currProfile;
                    }
                    name = newName;
                }
                profiles[name] = (ProfileOptions)currProfile.Clone();
            }
            if (SelectedProfile == null && DirtyProfiles.Count > 0)
                SelectedProfile = DirtyProfiles[0];
            var activeProfile = SelectedProfile?.General?.ProfileName ?? "";
            _project.Options.SetProfiles(profiles, activeProfile);
            return null;
        }

        private void OpenMacroEditor(object sender)
        {
            var editButton = (Button)sender;
            var options = editButton.DataContext;
            var propertyName = (string)editButton.Tag;
            ThreadHelper.JoinableTaskFactory.RunAsyncWithErrorHandling(() =>
                MacroEditor.EditObjectPropertyAsync(options, propertyName));
        }
    }
}
