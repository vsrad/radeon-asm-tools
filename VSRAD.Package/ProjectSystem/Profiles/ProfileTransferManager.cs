using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;
using VSRAD.Package.Options;

namespace VSRAD.Package.ProjectSystem.Profiles
{
    public sealed class ProfileTransferManager
    {
        public static Dictionary<string, ProfileOptions> Import(string path) =>
            JsonConvert.DeserializeObject<Dictionary<string, ProfileOptions>>(File.ReadAllText(path));

        public static void Export(IDictionary<string, ProfileOptions> profiles, string oath) =>
            File.WriteAllText(oath, JsonConvert.SerializeObject(profiles, Formatting.Indented));
    }
}
