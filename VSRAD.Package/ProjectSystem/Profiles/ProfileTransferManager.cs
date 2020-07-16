using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using VSRAD.Package.Options;

namespace VSRAD.Package.ProjectSystem.Profiles
{
    public sealed class ProfileTransferManager
    {
        public static Dictionary<string, ProfileOptions> Import(string path)
        {
            var json = JObject.Parse(File.ReadAllText(path));

            if (json.Properties().First().Value is JObject firstProfile && firstProfile.ContainsKey("Preprocessor"))
                return LegacyProfileImporter.ReadProfiles(json);

            return json.ToObject<Dictionary<string, ProfileOptions>>();
        }

        public static void Export(IDictionary<string, ProfileOptions> profiles, string oath) =>
            File.WriteAllText(oath, JsonConvert.SerializeObject(profiles, Formatting.Indented));
    }
}
