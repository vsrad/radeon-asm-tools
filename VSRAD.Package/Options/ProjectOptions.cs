using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using VSRAD.Package.Utils;

namespace VSRAD.Package.Options
{
    public sealed class ProjectOptions : DefaultNotifyPropertyChanged
    {
        public DebuggerOptions DebuggerOptions { get; } =
            new DebuggerOptions();

        public VisualizerOptions VisualizerOptions { get; } =
            new VisualizerOptions();

        public VisualizerAppearance VisualizerAppearance { get; } =
            new VisualizerAppearance();

        public DebugVisualizer.ColumnStylingOptions VisualizerColumnStyling { get; } =
            new DebugVisualizer.ColumnStylingOptions();

        #region Profiles
        private string _activeProfile = "Default";
        public string ActiveProfile { get => _activeProfile; set { if (value != null) SetField(ref _activeProfile, value, raiseIfEqual: true); } }

        [JsonIgnore]
        public bool HasProfiles => Profiles.Count > 0;
        [JsonIgnore]
        public ProfileOptions Profile => Profiles.TryGetValue(ActiveProfile, out var profile) ? profile : null;

        /// <summary>
        /// Provides read-only access to all existing profiles.
        /// To get the active profile, use the <see cref="Profile"/> property.
        /// To update profiles, use the <see cref="AddProfile"/> and <see cref="UpdateProfiles"/> methods. 
        /// </summary>
        public IObservableReadOnlyDictionary<string, ProfileOptions> Profiles { get; } =
            new ObservableDictionary<string, ProfileOptions>();

        public void AddProfile(string name, ProfileOptions profile)
        {
            var writeableProfiles = (IDictionary<string, ProfileOptions>)Profiles;
            writeableProfiles[name] = profile;
            ActiveProfile = name;
            RaisePropertyChanged(nameof(HasProfiles));
        }

        public delegate string ResolveImportNameConflict(string profileName);
        public void UpdateProfiles(IEnumerable<KeyValuePair<string, ProfileOptions>> updates, ResolveImportNameConflict nameConflictResolver)
        {
            var writeableProfiles = (IDictionary<string, ProfileOptions>)Profiles;
            foreach (var updateKv in updates.ToList())
            {
                var oldName = updateKv.Key;
                var newName = updateKv.Value.General.ProfileName;
                if (oldName != newName && Profiles.Keys.Contains(newName))
                    newName = nameConflictResolver(newName);
                if (string.IsNullOrWhiteSpace(newName))
                    newName = oldName;
                writeableProfiles[newName] = updateKv.Value;
                if (oldName != newName)
                    writeableProfiles.Remove(oldName);
                if (oldName == ActiveProfile)
                    ActiveProfile = newName;
            }
        }

        public void RemoveProfile(string name)
        {
            if (Profiles.Count == 1) throw new InvalidOperationException("Cannot remove the last profile without creating another.");
            ((IDictionary<string, ProfileOptions>)Profiles).Remove(name);
            ActiveProfile = Profiles.Keys.First();
        }
        #endregion

        #region Read/Write
        public static ProjectOptions Read(string path)
        {
            ProjectOptions options;
            try
            {
                options = JsonConvert.DeserializeObject<ProjectOptions>(File.ReadAllText(path),
                    new JsonSerializerSettings { DefaultValueHandling = DefaultValueHandling.Populate });
            }
            catch (Exception e)
            {
                options = new ProjectOptions();
                if (!(e is FileNotFoundException)) // File not found => creating a new project, don't show the error
                    Errors.ShowWarning($"An error has occurred while loading the project options: {e.Message} Proceeding with defaults.");
            }
            if (options.Profiles.Count > 0 && !options.Profiles.ContainsKey(options.ActiveProfile))
                options.ActiveProfile = options.Profiles.Keys.First();
            return options;
        }

        public void Write(string path)
        {
            try
            {
                File.WriteAllText(path, JsonConvert.SerializeObject(this, Formatting.Indented));
            }
            catch (SystemException e)
            {
                Errors.ShowWarning("Project options could not be saved: " + e.Message);
            }
        }
        #endregion
    }
}
