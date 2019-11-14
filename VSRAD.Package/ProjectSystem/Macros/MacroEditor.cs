using Microsoft.VisualStudio.ProjectSystem.Properties;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Data;
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

        public string EvaluatedValue => VSPackage.TaskFactory.Run(() => _evaluator.EvaluateAsync(_macroValue));

        public ICollectionView MacroListView { get; private set; } = new ListCollectionView(new List<KeyValuePair<string, string>>());

        private string _macroPreviewFilter = "$(rad";
        public string MacroPreviewFilter
        {
            get => _macroPreviewFilter;
            set
            {
                SetField(ref _macroPreviewFilter, value);
                MacroListView.Refresh();
            }
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
                var macros = await GetEnvironmentMacrosAsync(projectProperties, channel);
                await VSPackage.TaskFactory.SwitchToMainThreadAsync();
                MacroListView = new ListCollectionView(macros.ToList()) { Filter = FilterMacro };
                RaisePropertyChanged(nameof(MacroListView));
                RaisePropertyChanged(nameof(EvaluatedValue));
            });

        private bool FilterMacro(object macro)
        {
            var macroData = (KeyValuePair<string, string>)macro;
            return string.IsNullOrEmpty(MacroPreviewFilter)
                || macroData.Key.IndexOf(MacroPreviewFilter, StringComparison.OrdinalIgnoreCase) != -1
                || macroData.Value.IndexOf(MacroPreviewFilter, StringComparison.OrdinalIgnoreCase) != -1;
        }

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

                _evaluator.SetRemoteMacroPreviewList(remoteEnv.Variables);

                foreach (var entry in remoteEnv.Variables)
                    macros["$ENVR(" + entry.Key + ")"] = entry.Value;
            }
            catch (Exception) { } // Ignore remote environment fetch failures

            return macros;
        }
    }
}
