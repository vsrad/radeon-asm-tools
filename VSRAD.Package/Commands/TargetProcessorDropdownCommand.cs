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
                var predefinedTargetProcessors = _syntaxIntegration.GetTargetProcessorList().Select(p => p.TargetProcessor);
                var targetProcessorList = predefinedTargetProcessors.Prepend("Auto").Distinct().Append("Edit...").ToArray();
                Marshal.GetNativeVariantForObject(targetProcessorList, variantOut);
            }
            if (commandId == Constants.TargetProcessorDropdownId && variantOut != IntPtr.Zero)
            {
                string selectedTargetProcessor;
                if (_project.Options.TargetProcessor != null)
                {
                    selectedTargetProcessor = _project.Options.TargetProcessor;
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
                var selected = (string)Marshal.GetObjectForNativeVariant(variantIn);
                if (selected == "Edit...")
                    OpenProcessorListEditor();
                else if (selected == "Auto")
                    _project.Options.TargetProcessor = null;
                else
                    _project.Options.TargetProcessor = selected;
                _syntaxIntegration.NotifyTargetProcessorChanged();
            }
        }

        private void GetSelectedTargetProcessor(object sender, TargetProcessorSelectionEventArgs e)
        {
            e.Selection = Task.Run(async () =>
            {
                string sel = _project.Options.TargetProcessor ?? (await GetAutoTargetProcessorAsync());
                return _syntaxIntegration.GetTargetProcessorList().FirstOrDefault(p => p.TargetProcessor == sel);
            });
        }

        private async Task<string> GetAutoTargetProcessorAsync()
        {
            if (!_autoSelectedTargetProcessorRequested)
            {
                try
                {
                    var unevaluatedDefaultProcessor = _project.Options.DefaultTargetProcessor;
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

        public sealed class ProcessorItem : DefaultNotifyPropertyChanged
        {
            private string _value = "";
            public string Value { get => _value; set => SetField(ref _value, value); }
        }

        private void OpenProcessorListEditor()
        {
            var initProcessorList = _project.Options.TargetProcessors.Count > 0
                ? _project.Options.TargetProcessors.Select(a => new ProcessorItem { Value = a })
                : new[] { new ProcessorItem { Value = _project.Options.TargetProcessor } };
            var editor = new WpfMruEditor("Target Processor", initProcessorList)
            {
                CreateItem = () => new ProcessorItem { Value = "" },
                ValidateEditedItem = (_) => true,
                CheckHaveUnsavedChanges = (items) =>
                {
                    if (items.Count != _project.Options.TargetProcessors.Count)
                        return true;
                    for (int i = 0; i < items.Count; ++i)
                        if (((ProcessorItem)items[i]).Value != _project.Options.TargetProcessors[i])
                            return true;
                    return false;
                },
                SaveChanges = (items) =>
                {
                    _project.Options.TargetProcessors.Clear();
                    _project.Options.TargetProcessors.AddRange(items.Select(a => ((ProcessorItem)a).Value).Distinct());
                    _project.SaveOptions();
                }
            };
            editor.ShowModal();
        }
    }
}
