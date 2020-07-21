using Microsoft.VisualStudio.ProjectSystem.Properties;
using Microsoft.VisualStudio.Threading;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Data;
using VSRAD.Package.Options;
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

        public string EvaluatedValue => VSPackage.TaskFactory.Run(EvaluatePreviewAsync);

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

        public async Task LoadPreviewListAsync(IEnumerable<MacroItem> userMacros, IProjectProperties projectProperties, AsyncLazy<IReadOnlyDictionary<string, string>> remoteEnviornment)
        {
            var macroList = new List<KeyValuePair<string, string>>();

            var projectMacros = userMacros.Union(MacroListEditor.GetPredefinedMacroCollection());
            var projectMacroNames = projectMacros.Select(m => m.Name).Where(n => n != MacroName);
            var vsMacroNames = await projectProperties.GetPropertyNamesAsync().ConfigureAwait(false);

            foreach (var macro in projectMacroNames.Union(vsMacroNames))
            {
                var macroResult = await _evaluator.GetMacroValueAsync(macro).ConfigureAwait(false);
                if (macroResult.TryGetResult(out var macroValue, out var error))
                    macroList.Add(new KeyValuePair<string, string>("$(" + macro + ")", macroValue));
                else
                    macroList.Add(new KeyValuePair<string, string>("$(" + macro + ")", "<" + error.Message + ">"));
            }

            foreach (DictionaryEntry e in Environment.GetEnvironmentVariables())
                macroList.Add(new KeyValuePair<string, string>("$ENV(" + (string)e.Key + ")", (string)e.Value));

            await VSPackage.TaskFactory.SwitchToMainThreadAsync();
            MacroListView = new ListCollectionView(macroList) { Filter = FilterMacro };
            RaisePropertyChanged(nameof(MacroListView));
            RaisePropertyChanged(nameof(EvaluatedValue));
            Status = $"Editing {MacroName} (requesting remote environment variables...)";

            var remoteEnvVariables = await remoteEnviornment.GetValueAsync().ConfigureAwait(false);
            foreach (var e in remoteEnvVariables)
                macroList.Add(new KeyValuePair<string, string>("$ENVR(" + e.Key + ")", e.Value));

            await VSPackage.TaskFactory.SwitchToMainThreadAsync();
            MacroListView.Refresh();
            RaisePropertyChanged(nameof(EvaluatedValue));
            if (remoteEnvVariables.Count > 0)
                Status = $"Editing {MacroName} (showing local and remote environment variables)";
            else
                Status = $"Editing {MacroName} (showing local environment variables only)";
        }

        private async Task<string> EvaluatePreviewAsync()
        {
            var evalResult = await _evaluator.EvaluateAsync(_macroValue);
            if (evalResult.TryGetResult(out var macroValue, out var error))
                return macroValue;
            else
                return error.Message;
        }

        private bool FilterMacro(object macro)
        {
            var macroData = (KeyValuePair<string, string>)macro;
            return string.IsNullOrEmpty(MacroPreviewFilter)
                || macroData.Key.IndexOf(MacroPreviewFilter, StringComparison.OrdinalIgnoreCase) != -1
                || macroData.Value.IndexOf(MacroPreviewFilter, StringComparison.OrdinalIgnoreCase) != -1;
        }
    }
}
