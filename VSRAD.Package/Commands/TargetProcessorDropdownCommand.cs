using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.ProjectSystem;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Threading;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using VSRAD.Package.Options;
using VSRAD.Package.ProjectSystem;
using VSRAD.Package.ProjectSystem.Macros;
using VSRAD.Package.Server;
using VSRAD.Package.Utils;
using Task = System.Threading.Tasks.Task;

namespace VSRAD.Package.Commands
{
    [Export(typeof(ICommandHandler))]
    [AppliesTo(Constants.RadOrVisualCProjectCapability)]
    public sealed class TargetProcessorDropdownCommand : ICommandHandler, IDisposable
    {
        private readonly IProject _project;
        private readonly ICommunicationChannel _channel;
        private readonly ISyntaxIntegration _syntaxIntegration;

        private string _autoSelectedTargetProcessor;
        private bool _autoSelectedTargetProcessorRequested;

        [ImportingConstructor]
        public TargetProcessorDropdownCommand(IProject project, ICommunicationChannel channel, ISyntaxIntegration syntaxIntegration)
        {
            _project = project;
            _channel = channel;
            _channel.ConnectionStateChanged += RemoteConnectionChanged;
            _syntaxIntegration = syntaxIntegration;
            _syntaxIntegration.OnGetSelectedTargetProcessor += GetSelectedTargetProcessor;
        }

        public void Dispose()
        {
            _syntaxIntegration.OnGetSelectedTargetProcessor -= GetSelectedTargetProcessor;
        }

        public Guid CommandSet => Constants.TargetProcessorDropdownCommandSet;

        public OLECMDF GetCommandStatus(uint commandId, IntPtr commandText) =>
            OLECMDF.OLECMDF_ENABLED | OLECMDF.OLECMDF_SUPPORTED;

        public void Execute(uint commandId, uint commandExecOpt, IntPtr variantIn, IntPtr variantOut)
        {
            if (commandId == Constants.TargetProcessorDropdownListId && variantOut != IntPtr.Zero)
            {
                var items = EnumerateProcessorSyntaxItems().Select(p => p.FormattedValue).Append("Edit...").ToArray();
                Marshal.GetNativeVariantForObject(items, variantOut);
            }
            if (commandId == Constants.TargetProcessorDropdownId && variantOut != IntPtr.Zero)
            {
                string selectedTargetProcessor;
                if (_project.Options.SelectedTargetProcessor != null)
                {
                    selectedTargetProcessor = _project.Options.SelectedTargetProcessor.ToString();
                }
                else
                {
                    string autoTargetProcessor;
                    try
                    {
                        var cts = new CancellationTokenSource(millisecondsDelay: 300);
                        autoTargetProcessor = ThreadHelper.JoinableTaskFactory.RunAsync(() => GetAutoTargetProcessorAsync()).Join(cts.Token);
                    }
                    catch
                    {
                        autoTargetProcessor = null;
                    }
                    selectedTargetProcessor = $"Auto ({autoTargetProcessor ?? "Loading..."})";
                }
                Marshal.GetNativeVariantForObject(selectedTargetProcessor, variantOut);
            }
            if (commandId == Constants.TargetProcessorDropdownId && variantIn != IntPtr.Zero)
            {
                var optionIdx = (int)Marshal.GetObjectForNativeVariant(variantIn);
                var items = EnumerateProcessorSyntaxItems().ToList();
                if (optionIdx < items.Count)
                    _project.Options.SelectedTargetProcessor = items[optionIdx].Value;
                else // "Edit..."
                    OpenProcessorSyntaxItemEditor();
                _syntaxIntegration.NotifyTargetProcessorChanged();
            }
        }

        private void GetSelectedTargetProcessor(object sender, SelectedTargetProcessorEventArgs e)
        {
            e.Selection = Task.Run(async () =>
            {
                var processor = _project.Options.SelectedTargetProcessor != null
                    ? _project.Options.SelectedTargetProcessor.Value.Processor
                    : await GetAutoTargetProcessorAsync();
                var instructionSet = _project.Options.SelectedTargetProcessor != null
                    ? _project.Options.SelectedTargetProcessor.Value.InstructionSet
                    : _syntaxIntegration.GetPredefinedTargetProcessors().FirstOrDefault(p => p.Processor == processor || p.InstructionSet == processor).InstructionSet;
                return (processor, instructionSet);
            });
        }

        private async Task<string> GetAutoTargetProcessorAsync()
        {
            if (!_autoSelectedTargetProcessorRequested)
            {
                try
                {
                    var unevaluatedDefaultProcessor = _project.Options.Profile.General.DefaultTargetProcessor;
                    var remoteEnvironment = _project.Options.Profile.General.RunActionsLocally
                        ? null
                        : new AsyncLazy<IReadOnlyDictionary<string, string>>(() => _channel.GetRemoteEnvironmentAsync(default), ThreadHelper.JoinableTaskFactory);
                    var evaluator = new MacroEvaluator(
                        projectProperties: _project.GetProjectProperties(),
                        transientValues: new MacroEvaluatorTransientValues(0, "", "", "", "", ""),
                        remoteEnvironment, _project.Options.DebuggerOptions, _project.Options.Profile);
                    var evaluated = await evaluator.EvaluateAsync(unevaluatedDefaultProcessor);
                    if (!evaluated.TryGetResult(out _autoSelectedTargetProcessor, out _))
                        _autoSelectedTargetProcessor = null;
                }
                catch (ConnectionFailedException)
                {
                    _autoSelectedTargetProcessor = null;
                }
            }
            _autoSelectedTargetProcessorRequested = true;
            return _autoSelectedTargetProcessor;
        }

        private void RemoteConnectionChanged()
        {
            if (_channel.ConnectionState != ClientState.Connected)
            {
                _autoSelectedTargetProcessor = null;
                _autoSelectedTargetProcessorRequested = false;
            }
        }

        public sealed class ProcessorSyntaxItem : DefaultNotifyPropertyChanged
        {
            private TargetProcessor? _value;
            public TargetProcessor? Value { get => _value; set => SetField(ref _value, value); }

            private string _formattedValue = "";
            public string FormattedValue { get => _formattedValue; set => SetField(ref _formattedValue, value); }

            private bool _editable;
            public bool Editable { get => _editable; set => SetField(ref _editable, value); }
        }

        private IEnumerable<ProcessorSyntaxItem> EnumerateProcessorSyntaxItems()
        {
            var auto = new ProcessorSyntaxItem { Value = null, FormattedValue = "Auto", Editable = false };
            var predefined = _syntaxIntegration.GetPredefinedTargetProcessors()
                .Select(p => new ProcessorSyntaxItem { Value = p, FormattedValue = p.ToString(), Editable = false });
            var user = _project.Options.UserTargetProcessors
                .Select(p => new ProcessorSyntaxItem { Value = p, FormattedValue = p.ToString(), Editable = true });
            return predefined.Concat(user).Prepend(auto);
        }

        private void OpenProcessorSyntaxItemEditor()
        {
            var initProcessorSyntaxList = EnumerateProcessorSyntaxItems();
            var editor = new WpfMruEditor("Target Processor (Syntax)", initProcessorSyntaxList)
            {
                CreateItem = () => new ProcessorSyntaxItem { FormattedValue = "", Editable = true },
                ValidateEditedItem = (item) =>
                {
                    if (item is ProcessorSyntaxItem i && i.Editable)
                    {
                        if (string.IsNullOrEmpty(i.FormattedValue))
                            return false;

                        i.Value = new TargetProcessor(i.FormattedValue);
                    }
                    return true;
                },
                CheckHaveUnsavedChanges = (items) =>
                {
                    var customProcessors = items.Where(item => ((ProcessorSyntaxItem)item).Editable).Select(item => ((ProcessorSyntaxItem)item).Value).ToList();
                    if (customProcessors.Count != _project.Options.UserTargetProcessors.Count)
                        return true;
                    for (int i = 0; i < customProcessors.Count; ++i)
                    {
                        if (customProcessors[i] != _project.Options.UserTargetProcessors[i])
                            return true;
                    }
                    return false;
                },
                SaveChanges = (items) =>
                {
                    _project.Options.UserTargetProcessors.Clear();
                    foreach (ProcessorSyntaxItem item in items)
                    {
                        if (item.Editable && item.Value is TargetProcessor p && !string.IsNullOrEmpty(p.Processor))
                            _project.Options.UserTargetProcessors.Add(p);
                    }
                    _project.SaveOptions();
                }
            };
            editor.ShowModal();
        }
    }
}
