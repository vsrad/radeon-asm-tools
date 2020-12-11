using Microsoft.VisualStudio.ProjectSystem;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text.Tagging;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using VSRAD.Package.BuildTools;
using static VSRAD.BuildTools.IPCBuildResult;
using Task = System.Threading.Tasks.Task;

namespace VSRAD.Package.ProjectSystem
{
    public interface IErrorListManager
    {
        Task AddToErrorListAsync(IEnumerable<string> outputs);
    }

    [Export(typeof(IErrorListManager))]
    [AppliesTo(Constants.RadOrVisualCProjectCapability)]
    public sealed class ErrorListManager : IErrorListManager
    {
        private readonly SVsServiceProvider _serviceProvider;
        private readonly ErrorListProvider _errorListProvider;
        private readonly IBuildErrorProcessor _buildErrorProcessor;
        private readonly UnconfiguredProject _unconfiguredProject;
        private readonly IProject _project;

        [ImportingConstructor]
        public ErrorListManager(
            SVsServiceProvider serviceProvider,
            IBuildErrorProcessor buildErrorProcessor,
            IProject project,
            UnconfiguredProject unconfiguredProject)
        {
            _serviceProvider = serviceProvider;
            _errorListProvider = new ErrorListProvider(_serviceProvider);
            _buildErrorProcessor = buildErrorProcessor;
            _project = project;
            _unconfiguredProject = unconfiguredProject;

            _project.Unloaded += () => _errorListProvider.Tasks.Clear();
        }

        public async Task AddToErrorListAsync(IEnumerable<string> outputs)
        {
            _errorListProvider.Tasks.Clear();

            var errors = new List<ErrorTask>();
            var messages = await _buildErrorProcessor.ExtractMessagesAsync(outputs);
            foreach (var message in messages)
            {
                var document = string.IsNullOrEmpty(message.SourceFile) // make unclickable error otherwise it will refer to the project root
                    ? ""
                    : message.SourceFile.IndexOfAny(Path.GetInvalidPathChars()) == -1  
                        ? Path.Combine(_project.RootPath, message.SourceFile)
                        : "";
                var task = new ErrorTask
                {
                    Text = message.Text,
                    Document = document,
                    Line = message.Line - 1,
                    Column = message.Column,
                    ErrorCategory = ParseKind(message.Kind),
                    Category = TaskCategory.BuildCompile,
                };
                task.Navigate += (sender, e) =>
                {
                    task.Line++; // just because vs error list is dumb. inside it dec 1 and jumps to this line
                    _errorListProvider.Navigate(task, Guid.Parse(/*EnvDTE.Constants.vsViewKindCode*/"{7651A701-06E5-11D1-8EBD-00A0C90F26EA}"));
                    task.Line--;
                };
                _errorListProvider.Tasks.Add(task);
                errors.Add(task);
            }

            NotifyErrorTagger?.Invoke(errors);
        }

        private delegate void NotifyErrorTaggerDelegate(IEnumerable<ErrorTask> errorList);

        private bool _errorTaggerDelegateInitialized;
        private NotifyErrorTaggerDelegate _notifyErrorTagger;
        private NotifyErrorTaggerDelegate NotifyErrorTagger
        {
            get
            {
                if (!_errorTaggerDelegateInitialized)
                {
                    var taggers = _unconfiguredProject.Services.ExportProvider.GetExportedValues<IViewTaggerProvider>();
                    var syntaxTagger = taggers.FirstOrDefault(t => t.GetType().FullName == "VSRAD.Syntax.SyntaxHighlighter.ErrorHighlighter.ErrorHighlighterTaggerProvider");
                    if (syntaxTagger != null)
                    {
                        var notifyMethod = syntaxTagger.GetType().GetMethod("ErrorListUpdated");
                        if (notifyMethod != null)
                            _notifyErrorTagger = (NotifyErrorTaggerDelegate)Delegate.CreateDelegate(typeof(NotifyErrorTaggerDelegate), syntaxTagger, notifyMethod);
                    }
                    _errorTaggerDelegateInitialized = true;
                }
                return _notifyErrorTagger;
            }
        }

        private static TaskErrorCategory ParseKind(MessageKind kind)
        {
            switch (kind)
            {
                case MessageKind.Error: return TaskErrorCategory.Error;
                case MessageKind.Warning: return TaskErrorCategory.Warning;
                case MessageKind.Note: return TaskErrorCategory.Message;
                default: return TaskErrorCategory.Message;
            }
        }
    }
}
