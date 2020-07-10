﻿using Newtonsoft.Json;
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

        public SliceVisualizerOptions SliceVisualizerOptions { get; } =
            new SliceVisualizerOptions();

        public VisualizerAppearance VisualizerAppearance { get; } =
            new VisualizerAppearance();

        public DebugVisualizer.ColumnStylingOptions VisualizerColumnStyling { get; } =
            new DebugVisualizer.ColumnStylingOptions();

        public ProjectOptions() { }

        public ProjectOptions(DebuggerOptions debugger, VisualizerOptions visualizer, SliceVisualizerOptions slice, VisualizerAppearance appearance, DebugVisualizer.ColumnStylingOptions styling)
        {
            DebuggerOptions = debugger;
            VisualizerOptions = visualizer;
            SliceVisualizerOptions = slice;
            VisualizerAppearance = appearance;
            VisualizerColumnStyling = styling;
        }

        #region Profiles
        private string _activeProfile = "Default";
        public string ActiveProfile { get => _activeProfile; set { if (value != null) SetField(ref _activeProfile, value, raiseIfEqual: true); } }

        [JsonIgnore]
        public bool HasProfiles => Profiles.Count > 0;
        [JsonIgnore]
        public ProfileOptions Profile => Profiles.TryGetValue(ActiveProfile, out var profile) ? profile : null;

        public IObservableReadOnlyDictionary<string, ProfileOptions> Profiles { get; } =
            new ObservableDictionary<string, ProfileOptions>();

        public void SetProfiles(Dictionary<string, ProfileOptions> newProfiles, string activeProfile)
        {
            if (newProfiles.Count == 0)
                throw new InvalidOperationException("At least one profile is required.");

            var writeableProfiles = (IDictionary<string, ProfileOptions>)Profiles;
            writeableProfiles.Clear();
            foreach (var kv in newProfiles)
                writeableProfiles.Add(kv);

            ActiveProfile = activeProfile;
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
