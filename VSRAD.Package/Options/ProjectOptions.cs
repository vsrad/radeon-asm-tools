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
        public static ProjectOptions Read(string visualizerOptionsPath, string profilesOptionsPath, string oldOptionsPath)
        {
            Exception optionsException,
                      oldOptionsException = null,
                      profilesException,
                      oldProfilesException = null;
            var options = ReadProjectOptions(visualizerOptionsPath, out optionsException); // Try to parse .user.json
            if (options == null)
                options = ReadObsoleteProjectOptions(oldOptionsPath, out oldOptionsException);

            if (options == null && optionsException != null)
            {
                if (optionsException is FileNotFoundException)
                    if (oldOptionsException != null && !(oldOptionsException is FileNotFoundException))
                        Errors.ShowWarning($"An error has occurred while loading the options: {oldOptionsException.Message}\r\nProceeding with defaults.");
                    else
                        Errors.ShowWarning($"An error has occurred while loading the options: {optionsException.Message}\r\nProceeding with defaults.");

                options = new ProjectOptions();
            }

            var profiles = ReadProfiles(profilesOptionsPath, out profilesException);
            if (profiles == null)
                profiles = ReadObsoleteProfiles(oldOptionsPath, out oldProfilesException);

            if (profiles == null && profilesException != null)
            {
                if (profilesException is FileNotFoundException)
                    if (oldProfilesException != null && !(oldProfilesException is FileNotFoundException))
                        Errors.ShowWarning($"An error has occurred while loading the profiles: {oldProfilesException.Message}\r\nProceeding with defaults.");
                    else
                        Errors.ShowWarning($"An error has occurred while loading the profiles: {profilesException.Message}\r\nProceeding with defaults.");
            }

            if (profiles != null)
                options.SetProfiles(profiles, options.ActiveProfile);

            if (options.Profiles.Count > 0 && !options.Profiles.ContainsKey(options.ActiveProfile))
                options.ActiveProfile = options.Profiles.Keys.First();
            return options;
        }

        private static ProjectOptions ReadProjectOptions(string optionsPath, out Exception exception)
        {
            exception = null;
            try
            {
                var optionsJson = JObject.Parse(File.ReadAllText(optionsPath));
                return optionsJson.ToObject<ProjectOptions>(new JsonSerializer { DefaultValueHandling = DefaultValueHandling.Populate });
            }
            catch (Exception e)
            {
                exception = e;
                return null;
            }
        }

        private static ProjectOptions ReadObsoleteProjectOptions(string optionsPath, out Exception exception)
        {
            exception = null;
            try
            {
                return ProfileTransferManager.ImportObsoleteOptions(optionsPath);
            }
            catch (Exception e)
            {
                exception = e;
                return null;
            }
        }

        private static Dictionary<string, ProfileOptions> ReadProfiles(string profilesPath, out Exception exception)
        {
            exception = null;
            try
            {
                return ProfileTransferManager.Import(profilesPath);
            }
            catch (Exception e)
            {
                exception = e;
                return null;
            }
        }

        private static Dictionary<string, ProfileOptions> ReadObsoleteProfiles(string profilesPath, out Exception exception)
        {
            exception = null;
            try
            {
                return ProfileTransferManager.ImportObsolete(profilesPath);
            }
            catch (Exception e)
            {
                exception = e;
                return null;
            }
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
                if (File.ReadAllText(destPath) == contents) return; // We don't want to show the warning if config haven't changed
                                                                    // However, it's cheaper for us to rewrite the whole file than
                                                                    // to read it's contents to do this check, so we are doing it
                                                                    // only in case if the file is read-only to avoid extra warning
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
                    WriteAtomic(destPath, contents);
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
