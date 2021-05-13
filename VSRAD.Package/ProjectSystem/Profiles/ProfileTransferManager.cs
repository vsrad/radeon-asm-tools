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
            return json.ToObject<Dictionary<string, ProfileOptions>>();
        }

        public static Dictionary<string, ProfileOptions> ImportObsolete(string path)
        {
            var json = JObject.Parse(File.ReadAllText(path));
            // convert copy file steps to new format
            foreach (var profile in (JObject)json["Profiles"])
            {
                foreach (var action in profile.Value["Actions"])
                {
                    foreach (var step in action["Steps"])
                    {
                        if (step["Type"].ToString() != "CopyFile") continue;
                        var checkTimestamp = ((JObject)step).ContainsKey("CheckTimestamp") && (bool)step["CheckTimestamp"];
                        ((JObject)step).Add("IfNotModified", checkTimestamp ? "Fail" : "DoNotCopy");
                    }
                }
            }
            return json["Profiles"].ToObject<Dictionary<string, ProfileOptions>>();
        }

        public static ProjectOptions ImportObsoleteOptions(string path)
        {
            var json = JObject.Parse(File.ReadAllText(path));
            var debuggerOptions = json["DebuggerOptions"].ToObject<DebuggerOptions>();
            var visualizerOptions = json["VisualizerOptions"].ToObject<VisualizerOptions>();
            var sliceVisualizerOptions = json["SliceVisualizerOptions"].ToObject<SliceVisualizerOptions>();
            var visualizerAppearance = json["VisualizerAppearance"].ToObject<VisualizerAppearance>();
            var visualizerColumnStyling = json["VisualizerColumnStyling"].ToObject<DebugVisualizer.ColumnStylingOptions>();
            var activeProfile = json["ActiveProfile"].ToString();
            var breakArgs = json["DebuggerOptions"]["BreakArgs"].ToString();

            if (breakArgs != null)
                debuggerOptions.AppArgs2 = breakArgs;

            var options = new ProjectOptions(
                debuggerOptions,
                visualizerOptions,
                sliceVisualizerOptions,
                visualizerAppearance,
                visualizerColumnStyling
            );

            if (json["TargetHosts"] != null)
                foreach (var host in json["TargetHosts"].ToObject<List<string>>())
                    options.TargetHosts.Add(host);

            foreach (var host in options.TargetHosts)
                options.TargetHosts.Add(host);
            options.ActiveProfile = activeProfile;

            return options;
        }

        public static void Export(IDictionary<string, ProfileOptions> profiles, string oath) =>
            File.WriteAllText(oath, JsonConvert.SerializeObject(profiles, Formatting.Indented));
    }
}
