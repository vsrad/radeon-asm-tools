using Microsoft.VisualStudio.ProjectSystem.Properties;
using Microsoft.VisualStudio.Threading;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows.Data;
using VSRAD.Package.Utils;

namespace VSRAD.Package.ProjectSystem.Macros
{
    public sealed class MacroEditContext : DefaultNotifyPropertyChanged
    {
        private string _status = "Loading...";
        public string Status { get => _status; set => SetField(ref _status, value); }

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

        public bool MacroValueChanged => MacroValue != _initMacroValue;

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

        private readonly string _initMacroValue;
        private readonly IMacroEvaluator _evaluator;

        public MacroEditContext(string macroName, string macroValue, IMacroEvaluator evaluator)
        {
            MacroName = macroName;
            MacroValue = macroValue;
            _initMacroValue = macroValue;
            _evaluator = evaluator;
        }

        public void ResetChanges() => MacroValue = _initMacroValue;

        public void LoadPreviewListInBackground(IProjectProperties projectProperties, AsyncLazy<IReadOnlyDictionary<string, string>> remoteEnviornment) =>
            VSPackage.TaskFactory.RunAsyncWithErrorHandling(async () =>
            {
                var radMacroNames = typeof(RadMacros).GetConstantValues<string>().Where((name) => name != MacroName);
                var vsMacroNames = await projectProperties.GetPropertyNamesAsync().ConfigureAwait(false);
                var macroList = new List<KeyValuePair<string, string>>();
                foreach (var macroName in radMacroNames.Union(vsMacroNames))
                    macroList.Add(new KeyValuePair<string, string>("$(" + macroName + ")",
                        await _evaluator.GetMacroValueAsync(macroName).ConfigureAwait(false)));

                foreach (DictionaryEntry entry in Environment.GetEnvironmentVariables())
                    macroList.Add(new KeyValuePair<string, string>("$ENV(" + (string)entry.Key + ")",
                        (string)entry.Value));

                await VSPackage.TaskFactory.SwitchToMainThreadAsync();
                MacroListView = new ListCollectionView(macroList) { Filter = FilterMacro };
                RaisePropertyChanged(nameof(MacroListView));
                RaisePropertyChanged(nameof(EvaluatedValue));
                Status = $"Editing {MacroName} (requesting remote environment variables...)";

                var remoteEnv = await remoteEnviornment.GetValueAsync().ConfigureAwait(false);
                foreach (var env in remoteEnv)
                    macroList.Add(new KeyValuePair<string, string>("$ENVR(" + env.Key + ")", env.Value));

                await VSPackage.TaskFactory.SwitchToMainThreadAsync();
                MacroListView.Refresh();
                RaisePropertyChanged(nameof(EvaluatedValue));
                if (remoteEnv.Count > 0)
                    Status = $"Editing {MacroName} (showing local and remote environment variables)";
                else
                    Status = $"Editing {MacroName} (showing local environment variables only)";
            });

        private bool FilterMacro(object macro)
        {
            var macroData = (KeyValuePair<string, string>)macro;
            return string.IsNullOrEmpty(MacroPreviewFilter)
                || macroData.Key.IndexOf(MacroPreviewFilter, StringComparison.OrdinalIgnoreCase) != -1
                || macroData.Value.IndexOf(MacroPreviewFilter, StringComparison.OrdinalIgnoreCase) != -1;
        }
    }
}
