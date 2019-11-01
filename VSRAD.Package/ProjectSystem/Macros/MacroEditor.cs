using Microsoft.VisualStudio.ProjectSystem.Properties;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VSRAD.Package.Server;
using VSRAD.Package.Utils;

namespace VSRAD.Package.ProjectSystem.Macros
{
    public sealed class MacroEditor : DefaultNotifyPropertyChanged
    {
        public string MacroName { get; }

        private string _macroValue;
        public string MacroValue
        {
            get => _macroValue;
            set
            {
                SetField(ref _macroValue, value);
                RaisePropertyChanged(nameof(EvaluatedValue));
            }
        }

        public string EvaluatedValue => VSPackage.TaskFactory.Run(() => _evaluator.GetMacroValueAsync(_macroValue));

        public Dictionary<string, string> MacroPreviewList { get; set; } = new Dictionary<string, string>();

        private string _macroPreviewFilter;
        public string MacroPreviewFilter
        {
            get => _macroPreviewFilter;
            set => SetField(ref _macroPreviewFilter, value);
        }

        private readonly IMacroEvaluator _evaluator;

        public MacroEditor(string macroName, string macroValue, IMacroEvaluator evaluator)
        {
            MacroName = macroName;
            MacroValue = macroValue;
            _evaluator = evaluator;
        }

        public void LoadPreviewListInBackground(IProjectProperties projectProperties, ICommunicationChannel channel) =>
            VSPackage.TaskFactory.RunAsync(async () =>
            {
                MacroPreviewList = await GetEnvironmentMacrosAsync(projectProperties, channel);
                await VSPackage.TaskFactory.SwitchToMainThreadAsync();
                RaisePropertyChanged(nameof(MacroPreviewList));
            });

        private async Task<Dictionary<string, string>> GetEnvironmentMacrosAsync(IProjectProperties properties, ICommunicationChannel channel)
        {
            var vsMacroNames = await properties.GetPropertyNamesAsync().ConfigureAwait(false);
            var radMacroNames = typeof(RadMacros).GetConstantValues<string>();

            var macros = new Dictionary<string, string>();

            foreach (var macroName in vsMacroNames.Union(radMacroNames))
                macros["$(" + macroName + ")"] = await _evaluator.GetMacroValueAsync(macroName).ConfigureAwait(false);

            foreach (DictionaryEntry entry in Environment.GetEnvironmentVariables())
                macros["$ENV(" + (string)entry.Key + ")"] = (string)entry.Value;

            try
            {
                var remoteEnv = await channel.SendWithReplyAsync<DebugServer.IPC.Responses.EnvironmentVariablesListed>(
                    new DebugServer.IPC.Commands.ListEnvironmentVariables()).ConfigureAwait(false);

                foreach (var entry in remoteEnv.Variables)
                    macros["$ENVR(" + entry.Key + ")"] = entry.Value;
            }
            catch (Exception) { } // Ignore remote environment fetch failures

            return macros;
        }
    }
}
