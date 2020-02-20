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
    public sealed class MacroEditor : DefaultNotifyPropertyChanged
    {
        private string _status = "Loading...";
        public string Status { get => _status; set => SetField(ref _status, value); }

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

        private readonly string _macroName;
        private readonly IMacroEvaluator _evaluator;

        public MacroEditor(string macroName, string macroValue, IMacroEvaluator evaluator)
        {
            _macroName = macroName;
            MacroValue = macroValue;
            _evaluator = evaluator;
        }

        public void LoadPreviewListInBackground(IProjectProperties projectProperties, AsyncLazy<IReadOnlyDictionary<string, string>> remoteEnviornment) =>
            VSPackage.TaskFactory.RunAsyncWithErrorHandling(async () =>
            {
                var radMacroNames = typeof(RadMacros).GetConstantValues<string>().Where((name) => name != _macroName);
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
                Status = $"Editing {_macroName} (requesting remote environment variables...)";

                var remoteEnv = await remoteEnviornment.GetValueAsync().ConfigureAwait(false);
                foreach (var env in remoteEnv)
                    macroList.Add(new KeyValuePair<string, string>("$ENVR(" + env.Key + ")", env.Value));

                await VSPackage.TaskFactory.SwitchToMainThreadAsync();
                MacroListView.Refresh();
                RaisePropertyChanged(nameof(EvaluatedValue));
                if (remoteEnv.Count > 0)
                    Status = $"Editing {_macroName} (showing local and remote environment variables)";
                else
                    Status = $"Editing {_macroName} (showing local environment variables only)";
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
