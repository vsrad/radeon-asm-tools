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

        public DebugVisualizer.ColumnStylingOptions VisualizerColumnStyling { get; } =
            new DebugVisualizer.ColumnStylingOptions();

        #region Profiles
        private string _activeProfile = "Default";
        public string ActiveProfile { get => _activeProfile; set { if (value != null) SetField(ref _activeProfile, value); } }

        [JsonIgnore]
        public ProfileOptions Profile => Profiles[ActiveProfile];

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
        }

        public void UpdateProfiles(IEnumerable<KeyValuePair<string, ProfileOptions>> updates)
        {
            var writeableProfiles = (IDictionary<string, ProfileOptions>)Profiles;
            foreach (var updateKv in updates)
            {
                writeableProfiles[updateKv.Key] = updateKv.Value;
                if (updateKv.Key == ActiveProfile)
                    RaisePropertyChanged(nameof(ActiveProfile));
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
            if (options.Profiles.Count == 0)
                options.AddProfile("Default", new ProfileOptions());
            else if (!options.Profiles.ContainsKey(options.ActiveProfile))
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
                Errors.ShowCritical(e.Message);
            }
        }
        #endregion
    }
}
