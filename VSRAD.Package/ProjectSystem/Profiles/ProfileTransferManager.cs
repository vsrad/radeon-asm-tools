using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using VSRAD.Package.Options;
using static VSRAD.Package.Options.ProjectOptions;

namespace VSRAD.Package.ProjectSystem.Profiles
{
    public sealed class ProfileTransferManager
    {
        private readonly ProjectOptions _projectOptions;
        private readonly ResolveImportNameConflict _nameConflictResolver;

        public ProfileTransferManager(ProjectOptions projectOptions, ResolveImportNameConflict nameConflictResolver)
        {
            _projectOptions = projectOptions;
            _nameConflictResolver = nameConflictResolver;
        }

        public void Export(string targetPath) =>
            File.WriteAllText(targetPath, JsonConvert.SerializeObject(_projectOptions.Profiles, Formatting.Indented));

        public void Import(string sourcePath) =>
            _projectOptions.UpdateProfiles(EnumerateInFile(sourcePath), _nameConflictResolver);

        private IEnumerable<KeyValuePair<string, ProfileOptions>> EnumerateInFile(string path)
        {
            var profiles = JsonConvert.DeserializeObject<Dictionary<string, ProfileOptions>>(File.ReadAllText(path));

            foreach (var profileKv in profiles)
            {
                string profileName = profileKv.Key;

                if (_projectOptions.Profiles.ContainsKey(profileName))
                    profileName = _nameConflictResolver(profileName);

                if (profileName != null)
                    yield return new KeyValuePair<string, ProfileOptions>(profileName, profileKv.Value);
            }
        }
    }
}
