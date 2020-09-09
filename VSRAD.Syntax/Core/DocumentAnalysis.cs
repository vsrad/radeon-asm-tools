using Microsoft.VisualStudio.Text;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VSRAD.Syntax.Core.Tokens;
using Task = System.Threading.Tasks.Task;
using VSRAD.Syntax.Core.Parser;
using VSRAD.Syntax.Core.Helper;

namespace VSRAD.Syntax.Core
{
    internal class DocumentAnalysis : IDocumentAnalysis
    {
        private readonly IDocument _document;
        private readonly IParser _parser;
        private readonly FixedSizeDictionary<ITextSnapshot, Task<IAnalysisResult>> _resultsRequests;
        private CancellationTokenSource _cts;

        public event AnalysisUpdatedEventHandler AnalysisUpdated;

        public DocumentAnalysis(IDocument document, IDocumentTokenizer tokenizer, IParser parser)
        {
            _document = document;
            _parser = parser;
            _cts = new CancellationTokenSource();
            _resultsRequests = new FixedSizeDictionary<ITextSnapshot, Task<IAnalysisResult>>(100);

            tokenizer.TokenizerUpdated += TokenizerUpdated;
            TokenizerUpdated(tokenizer.CurrentResult);
        }

        public async Task<IAnalysisResult> GetAnalysisResultAsync(ITextSnapshot textSnapshot)
        {
            if (_resultsRequests.TryGetValue(textSnapshot, out var task))
                return await task.ConfigureAwait(false);

            throw new NotImplementedException();
        }

        private void TokenizerUpdated(TokenizerResult tokenizerResult)
        {
            _cts.Cancel();
            _cts = new CancellationTokenSource();

            _resultsRequests.TryAddValue(tokenizerResult.Snapshot, 
                () => RunAnalysisAsync(tokenizerResult, _cts.Token));
        }

        private Task<IAnalysisResult> RunAnalysisAsync(TokenizerResult tokenizerResult, CancellationToken cancellationToken) =>
            Task.Run(async () => await RunParserAsync(tokenizerResult, cancellationToken));

        private async Task<IAnalysisResult> RunParserAsync(TokenizerResult tokenizerResult, CancellationToken cancellationToken)
        {
            var blocks = await _parser.RunAsync(_document, tokenizerResult.Snapshot, tokenizerResult.Tokens, cancellationToken).ConfigureAwait(false);
            var rootBlock = blocks[0];

            var includes = rootBlock.Tokens
                .Where(t => t.Type == RadAsmTokenType.Include)
                .Cast<IncludeToken>()
                .Select(i => i.Document)
                .ToList();

            var analysisResult = new AnalysisResult(rootBlock, blocks, includes, tokenizerResult.Snapshot);

            InvokeUpdate(analysisResult);
            return analysisResult;
        }

        private void InvokeUpdate(IAnalysisResult analysisResult) =>
            AnalysisUpdated?.Invoke(analysisResult);
    }
}
