using System;
using System.ComponentModel.Design;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.Shell;
using Task = System.Threading.Tasks.Task;
using VSRAD.Syntax.Helpers;
using System.Linq;
using VSRAD.Syntax.Options;
using VSRAD.Syntax.Core;
using VSRAD.Syntax.Core.Tokens;
using VSRAD.Syntax.IntelliSense;

namespace VSRAD.Syntax.FunctionList
{
    [Guid(Constants.FunctionListToolWindowPaneGuid)]
    public class FunctionList : ToolWindowPane
    {
        private const string CaptionName = "Function list";
        private FunctionListControl FunctionListControl;
        private Lazy<INavigationTokenService> _navigationTokenService;

        public FunctionList() : base(null)
        {
            Caption = CaptionName;
        }

        protected override void Initialize()
        {
            var documentFactory = Syntax.Package.Instance.GetMEFComponent<IDocumentFactory>();
            var optionsProvider = Syntax.Package.Instance.GetMEFComponent<OptionsProvider>();
            _navigationTokenService = new Lazy<INavigationTokenService>(() => Syntax.Package.Instance.GetMEFComponent<INavigationTokenService>());
            var commandService = GetService(typeof(IMenuCommandService)) as OleMenuCommandService;

            FunctionListControl = new FunctionListControl(commandService);
            Content = FunctionListControl;

            documentFactory.DocumentCreated += DocumentCreated;
            documentFactory.DocumentDisposed += DocumentDisposed;
            documentFactory.ActiveDocumentChanged += ActiveDocumentChanged;
            optionsProvider.OptionsUpdated += OptionsUpdated;
        }

        private void OptionsUpdated(OptionsProvider sender)
        {
            FunctionListControl.Autoscroll = sender.Autoscroll;
            FunctionListControl.SortState = sender.SortOptions;
        }

        private void DocumentCreated(IDocument document)
        {
            document.DocumentAnalysis.AnalysisUpdated += (result) => UpdateFunctionList(document, result);
            UpdateFunctionList(document);
        }

        private void DocumentDisposed(IDocument document) =>
            document.DocumentAnalysis.AnalysisUpdated -= (result) => UpdateFunctionList(document, result);

        private void ActiveDocumentChanged(IDocument activeDocument)
        {
            if (activeDocument == null)
                FunctionListControl.ClearList();
            else
                UpdateFunctionList(activeDocument);
        }

        private async Task UpdateFunctionListAsync(IDocument document, IAnalysisResult analysisResult)
        {
            var tokens = analysisResult.Scopes
                .SelectMany(s => s.Tokens)
                .Where(t => t.Type == RadAsmTokenType.Label || t.Type == RadAsmTokenType.FunctionName)
                .Select(t => _navigationTokenService.Value.CreateToken(t, document))
                .Select(t => new FunctionListItem(t));

            await FunctionListControl.UpdateListAsync(tokens);
        }

        private void UpdateFunctionList(IDocument document) =>
            Task.Run(async () =>
            {
                var analysisResult = await document.DocumentAnalysis.GetAnalysisResultAsync(document.CurrentSnapshot);
                await UpdateFunctionListAsync(document, analysisResult);
            }).RunAsyncWithoutAwait();

        private void UpdateFunctionList(IDocument document, IAnalysisResult analysisResult) =>
            Task.Run(async () => await UpdateFunctionListAsync(document, analysisResult))
                .RunAsyncWithoutAwait();
    }
}
