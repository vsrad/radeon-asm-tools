using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using VSRAD.Package.ProjectSystem.Profiles;
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

        [JsonConverter(typeof(MruCollection<string>.Converter))]
        public MruCollection<string> TargetHosts { get; } =
            new MruCollection<string>();

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
        [JsonIgnore]
        public IReadOnlyDictionary<string, ProfileOptions> Profiles { get; private set; } =
            new Dictionary<string, ProfileOptions>();

        public void SetProfiles(Dictionary<string, ProfileOptions> newProfiles, string activeProfile)
        {
            Profiles = newProfiles;
            ActiveProfile = activeProfile;
            RaisePropertyChanged(nameof(Profiles));
            RaisePropertyChanged(nameof(HasProfiles));
        }

        public void UpdateActiveProfile(ProfileOptions newProfile)
        {
            ((Dictionary<string, ProfileOptions>)Profiles)[ActiveProfile] = newProfile;
            RaisePropertyChanged(nameof(Profiles));
            RaisePropertyChanged(nameof(ActiveProfile));
        }
        #endregion

        #region Read/Write
        public static ProjectOptions Read(string visualizerOptionsPath, string profilesOptionsPath)
        {
            ProjectOptions options = null;
            try
            {
                var optionsJson = JObject.Parse(File.ReadAllText(visualizerOptionsPath));
                var profilesJson = JObject.Parse(File.ReadAllText(profilesOptionsPath));
                options = optionsJson.ToObject<ProjectOptions>(new JsonSerializer { DefaultValueHandling = DefaultValueHandling.Populate });
                var profiles = profilesJson.ToObject<Dictionary<string, ProfileOptions>>(new JsonSerializer { DefaultValueHandling = DefaultValueHandling.Populate });
                options.SetProfiles(profiles, options.ActiveProfile);
            }
            catch (FileNotFoundException) { } // Don't show an error if the configuration file is missing, just load defaults
            catch (Exception e)
            {
                Errors.ShowWarning($"An error has occurred while loading the project options: {e.Message}\r\nProceeding with defaults.");
            }
            if (options == null) // Note that DeserializeObject can return null even on success (e.g. if the file is empty)
                options = new ProjectOptions();
            if (options.Profiles.Count > 0 && !options.Profiles.ContainsKey(options.ActiveProfile))
                options.ActiveProfile = options.Profiles.Keys.First();
            return options;
        }

        public static ProjectOptions ReadLegacy(string path)
        {
            try
            {
                var legacyJson = JObject.Parse(File.ReadAllText(path));
                return LegacyProfileImporter.ReadProjectOptions(legacyJson);
            }
            catch (Exception e)
            {
                Errors.ShowWarning($"A legacy project options file was found but could not be converted: {e.Message}\r\nYou can transfer your configuration manually from {path}");
                return new ProjectOptions();
            }
        }

        public void Write(string visualConfigPath, string profilesConfigPath)
        {
            var serializedOptions = JsonConvert.SerializeObject(this, Formatting.Indented);
            var serializedProfiles = JsonConvert.SerializeObject(Profiles, Formatting.Indented);
            try
            {
                WriteAtomic(visualConfigPath, serializedOptions);
                WriteAtomic(profilesConfigPath, serializedProfiles);
            }
            catch (UnauthorizedAccessException)
            {
                DialogResult res = MessageBox.Show($"RAD Debug is unable to save configuration, because {profilesConfigPath} is read-only. Make it writable?", "Warning", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning);
                if (res == DialogResult.OK)
                {
                    try
                    {
                        File.SetAttributes(profilesConfigPath, FileAttributes.Normal);
                    }
                    catch (Exception ex)
                    {
                        Errors.ShowWarning("Cannot make file writable: " + ex.Message);
                        return;
                    }
                    WriteAtomic(profilesConfigPath, serializedOptions);
                }
            }
            catch (SystemException e)
            {
                Errors.ShowWarning("Project options could not be saved: " + e.Message);
            }
        }

        private static void WriteAtomic(string destPath, string contents)
        {
            // Source and destination files for File.Replace need to be located on the same volume,
            // and since the project can be located anywhere, we can't use Path.GetTempFileName
            var tmpPath = destPath + ".tmp";

            // Specify WriteThrough to skip caching and write directly to disk
            using (var tmp = File.Create(tmpPath, 4096, FileOptions.WriteThrough))
            {
                var data = Encoding.UTF8.GetBytes(contents);
                tmp.Write(data, 0, data.Length);
            }

            try
            {
                // Atomically replace file contents
                File.Replace(tmpPath, destPath, null);
            }
            catch (FileNotFoundException)
            {
                // Destination file does not exist
                File.Move(tmpPath, destPath);
            }
        }
        #endregion
    }
}
