using Microsoft.VisualStudio.ProjectSystem;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using VSRAD.Package.BuildTools;
using static VSRAD.BuildTools.IPCBuildResult;

namespace VSRAD.Package.ProjectSystem
{
    public interface IErrorListManager
    {
        Task<(int ErrorCount, int WarningCount, int MessageCount)> AddToErrorListAsync(IEnumerable<string> outputs);
    }

    [Export(typeof(IErrorListManager))]
    [AppliesTo(Constants.RadOrVisualCProjectCapability)]
    public sealed class ErrorListManager : IErrorListManager
    {
        private readonly SVsServiceProvider _serviceProvider;
        private readonly ErrorListProvider _errorListProvider;
        private readonly IBuildErrorProcessor _buildErrorProcessor;
        private readonly IProject _project;

        [ImportingConstructor]
        public ErrorListManager(SVsServiceProvider serviceProvider, IBuildErrorProcessor buildErrorProcessor, IProject project)
        {
            _serviceProvider = serviceProvider;
            _errorListProvider = new ErrorListProvider(_serviceProvider);
            _buildErrorProcessor = buildErrorProcessor;
            _project = project;

            _project.Unloaded += () => _errorListProvider.Tasks.Clear();
        }

        public async Task<(int ErrorCount, int WarningCount, int MessageCount)> AddToErrorListAsync(IEnumerable<string> outputs)
        {
            _errorListProvider.Tasks.Clear();

            var (errorCount, warningCount, messageCount) = (0, 0, 0);

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
                    task.Line++; // Workaround for ErrorListProvider.Navigate
                    _errorListProvider.Navigate(task, Guid.Parse(EnvDTE.Constants.vsViewKindCode));
                    task.Line--; // Workaround for ErrorListProvider.Navigate
                };
                _errorListProvider.Tasks.Add(task);
                errors.Add(task);

                if (task.ErrorCategory == TaskErrorCategory.Error)
                    errorCount++;
                else if (task.ErrorCategory == TaskErrorCategory.Warning)
                    warningCount++;
                else if (task.ErrorCategory == TaskErrorCategory.Message)
                    messageCount++;
            }

            NotifyErrorTagger?.Invoke(errors);
            return (errorCount, warningCount, messageCount);
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
                    var syntaxErrorTagger = _project.GetExportByMetadataAndType<IViewTaggerProvider, IContentTypeMetadata>(
                        m => m.ContentTypes.Contains("RadeonAsmSyntax"),
                        e => e.GetType().FullName == "VSRAD.Syntax.SyntaxHighlighter.ErrorHighlighter.ErrorHighlighterTaggerProvider");
                    if (syntaxErrorTagger != null)
                    {
                        var notifyMethod = syntaxErrorTagger.GetType().GetMethod("ErrorListUpdated");
                        if (notifyMethod != null)
                            _notifyErrorTagger = (NotifyErrorTaggerDelegate)Delegate.CreateDelegate(typeof(NotifyErrorTaggerDelegate), syntaxErrorTagger, notifyMethod);
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
