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

        [JsonConverter(typeof(MruCollection<HostItem>.Converter))]
        public MruCollection<HostItem> TargetHosts { get; } =
            new MruCollection<HostItem>();

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

        private string _remoteMachine = "127.0.0.1";
        public string RemoteMachine { get => _remoteMachine; set => SetField(ref _remoteMachine, value); }

        private int _port = 9339;
        public int Port { get => _port; set => SetField(ref _port, value); }

        [JsonIgnore]
        public ServerConnectionOptions Connection => new ServerConnectionOptions(RemoteMachine, Port);

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
        public static ProjectOptions Read(string userOptionsPath, string profilesOptionsPath, string oldConfigPath)
        {
            ProjectOptions options = null;
            // Handle options and profiles loading in separate try blocks since we want them
            // to load indepentently, i.e use default configs if corresponding file is missing
            // in each case without affecting one another
            try // Try to load .user.json in the first place
            {
                var optionsJson = JObject.Parse(File.ReadAllText(userOptionsPath));
                options = optionsJson.ToObject<ProjectOptions>(new JsonSerializer { DefaultValueHandling = DefaultValueHandling.Populate });
            }
            catch (FileNotFoundException)
            {
                try // Try to load options from .conf.json if .user.json is missing
                {
                    options = ProfileTransferManager.ImportObsoleteOptions(oldConfigPath);
                }
                catch (FileNotFoundException) { } // Don't show an error if the configuration file is missing, just load defaults
                catch (Exception e)
                {
                    Errors.ShowWarning($"An error has occurred while loading the project options: {e.Message}\r\nProceeding with defaults.");
                }
            }
            catch (Exception e)
            {
                Errors.ShowWarning($"An error has occurred while loading the project options: {e.Message}\r\nProceeding with defaults.");
            }

            if (options == null) // Note that DeserializeObject can return null even on success (e.g. if the file is empty)
                options = new ProjectOptions();

            try // Try to load .profiles.json in the first place
            {
                var profiles = ProfileTransferManager.Import(profilesOptionsPath);
                options.SetProfiles(profiles, options.ActiveProfile);
            }
            catch (FileNotFoundException)
            {
                try // Try to load profiles from .conf.json if .profiles.json is missing
                {
                    var profiles = ProfileTransferManager.ImportObsolete(oldConfigPath);
                    options.SetProfiles(profiles, options.ActiveProfile);
                }
                catch (FileNotFoundException) { } // Don't show an error if the configuration file is missing, just load defaults
                catch (Exception e)
                {
                    Errors.ShowWarning($"An error has occurred while loading the profiles: {e.Message}\r\nProceeding with defaults.");
                }
            }
            catch (Exception e)
            {
                Errors.ShowWarning($"An error has occurred while loading the profiles: {e.Message}\r\nProceeding with defaults.");
            }

            if (options.Profiles.Count > 0 && !options.Profiles.ContainsKey(options.ActiveProfile))
                options.ActiveProfile = options.Profiles.Keys.First();
            return options;
        }

        public void Write(string visualConfigPath, string profilesConfigPath)
        {
            var serializedOptions = JsonConvert.SerializeObject(this, Formatting.Indented);
            var serializedProfiles = JsonConvert.SerializeObject(Profiles, Formatting.Indented);

            WriteAtomicWithErrorChecking(visualConfigPath, serializedOptions);
            WriteAtomicWithErrorChecking(profilesConfigPath, serializedProfiles);
        }

        private static void WriteAtomicWithErrorChecking(string destPath, string contents)
        {
            try
            {
                WriteAtomic(destPath, contents);
            }
            catch (UnauthorizedAccessException)
            {
                try
                {
                    if (File.ReadAllText(destPath) == contents) return; // We don't want to show the warning if config haven't changed
                                                                        // However, it's cheaper for us to rewrite the whole file than
                                                                        // to read it's contents to do this check, so we are doing it
                                                                        // only in case if the file is read-only to avoid extra warning
                }
                catch (Exception) { }; // If we can't read the profile, treat it as changed
                DialogResult res = MessageBox.Show($"RAD Debug is unable to save configuration, because {destPath} is read-only. Make it writable?", "Warning", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning);
                if (res == DialogResult.OK)
                {
                    try
                    {
                        File.SetAttributes(destPath, FileAttributes.Normal);
                    }
                    catch (Exception ex)
                    {
                        Errors.ShowWarning("Cannot make file writable: " + ex.Message);
                        return;
                    }
                    try
                    {
                        WriteAtomic(destPath, contents);
                    }
                    catch (Exception ex)
                    {
                        Errors.ShowWarning("Project options could not be saved: " + ex.Message);
                        return;
                    }
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
            catch (Exception)
            {
                // In case when we can't perform Move for a reasons that we do not handle in
                // this function (typically destPath is read-only) we want to delete .tmp
                // file, otherwise it will stay in the directory
                File.Delete(tmpPath);
                throw;
            }
        }
        #endregion
    }
}
