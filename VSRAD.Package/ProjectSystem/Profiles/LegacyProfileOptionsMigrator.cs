using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Linq;
using VSRAD.Package.Options;

namespace VSRAD.Package.ProjectSystem.Profiles
{
    /// <summary>
    /// Converts the following unsupported options in the current configuration format:
    /// <list type="bullet">
    /// <item><c>DebuggerProfileOptions</c>, used before the introduction of <c>ReadDebugData</c></item>
    /// </list>
    /// For reading legacy configuration files, see <c>LegacyProfileImporter</c>.
    /// </summary>
    public static class LegacyProfileOptionsMigrator
    {
        public static void ConvertOldOptionsIfPresent(JObject conf)
        {
            foreach (JObject profile in ((JObject)conf["Profiles"]).PropertyValues())
            {
                if (profile.TryGetValue("Debugger", out var oldDebuggerConf))
                {
                    var steps = ConvertDebuggerActionToSteps((JObject)oldDebuggerConf);
                    var debugAction = new ActionProfileOptions { Name = "Debug" };
                    foreach (var step in steps)
                        debugAction.Steps.Add(step);

                    var existingActions = profile["Actions"].ToObject<List<ActionProfileOptions>>();
                    while (existingActions.Any(a => a.Name == debugAction.Name))
                        debugAction.Name += " (Old)";
                    existingActions.Add(debugAction);

                    profile["Actions"] = JArray.FromObject(existingActions);

                    var menuCommands = profile.TryGetValue("MenuCommands", out var mcmds)
                        ? mcmds.ToObject<MenuCommandProfileOptions>()
                        : new MenuCommandProfileOptions();
                    menuCommands.DebugAction = debugAction.Name;
                    profile["MenuCommands"] = JObject.FromObject(menuCommands);
                }
            }
        }

        private static List<IActionStep> ConvertDebuggerActionToSteps(JObject conf)
        {
            var serializer = new JsonSerializer();
            serializer.Converters.Add(new ActionStepJsonConverter());
            var steps = conf["Steps"].ToObject<List<IActionStep>>(serializer);
            var outputFile = conf["OutputFile"].ToObject<BuiltinActionFile>();
            var watchesFile = conf["WatchesFile"].ToObject<BuiltinActionFile>();
            var dispatchParamsFile = conf["StatusFile"].ToObject<BuiltinActionFile>();
            var binaryOutput = (bool)conf["BinaryOutput"];
            var outputOffset = (int)conf["OutputOffset"];
            var readDebugData = new ReadDebugDataStep(outputFile, watchesFile, dispatchParamsFile, binaryOutput, outputOffset);
            steps.Add(readDebugData);
            return steps;
        }
    }
}
