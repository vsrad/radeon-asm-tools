﻿using Newtonsoft.Json;
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

        public IReadOnlyDictionary<string, ProfileOptions> Profiles { get; private set; } =
            new Dictionary<string, ProfileOptions>();

        public void SetProfiles(Dictionary<string, ProfileOptions> newProfiles, string activeProfile)
        {
            Profiles = newProfiles;
            ActiveProfile = activeProfile;
            RaisePropertyChanged(nameof(Profiles));
            RaisePropertyChanged(nameof(HasProfiles));
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
                    Errors.ShowWarning($"An error has occurred while loading the project options: {e.Message}\r\nProceeding with defaults.");
            }
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

        public void Write(string path)
        {
            var serializedOptions = JsonConvert.SerializeObject(this, Formatting.Indented);
            try
            {
                WriteAtomic(path, serializedOptions);
            }
            catch (UnauthorizedAccessException)
            {
                DialogResult res = MessageBox.Show($"RAD Debug is unable to save configuration, because {path} is read-only. Make it writable?", "Warning", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning);
                if (res == DialogResult.OK)
                {
                    try
                    {
                        File.SetAttributes(path, FileAttributes.Normal);
                    }
                    catch (Exception ex)
                    {
                        Errors.ShowWarning("Cannot make file writable: " + ex.Message);
                        return;
                    }
                    WriteAtomic(path, serializedOptions);
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
