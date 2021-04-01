using Microsoft.VisualStudio.Text;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Task = System.Threading.Tasks.Task;
using VSRAD.Syntax.Core.Parser;
using VSRAD.Syntax.Core.Helper;

namespace VSRAD.Syntax.Core
{
    internal class DocumentAnalysis : IDocumentAnalysis
    {
        private readonly IDocument _document;
        private readonly IParser _parser;
        private readonly OrderedFixedSizeDictionary<ITextSnapshot, Task<IAnalysisResult>> _resultsRequests;
        private readonly IDocumentTokenizer _tokenizer;

        public IAnalysisResult CurrentResult { get; private set; }
        public event AnalysisUpdatedEventHandler AnalysisUpdated;

        public DocumentAnalysis(IDocument document, IDocumentTokenizer tokenizer, IParser parser)
        {
            _document = document;
            _tokenizer = tokenizer;
            _parser = parser;
            _resultsRequests = new OrderedFixedSizeDictionary<ITextSnapshot, Task<IAnalysisResult>>(10);

            tokenizer.TokenizerUpdated += TokenizerUpdated;
            TokenizerUpdated(tokenizer.CurrentResult, CancellationToken.None);
        }

        public async Task<IAnalysisResult> GetAnalysisResultAsync(ITextSnapshot textSnapshot)
        {
            if (_resultsRequests.TryGetValue(textSnapshot, out var task))
                return await task.ConfigureAwait(false);

            throw new TaskCanceledException("Buffer changes have not yet been processed");
        }

        public void Rescan(RescanReason rescanReason, CancellationToken cancellationToken)
        {
            if (_tokenizer.CurrentResult == null) return;
            ForceRescan(_tokenizer.CurrentResult, rescanReason, cancellationToken);
        }

        public void OnDestroy()
        {
            _tokenizer.TokenizerUpdated -= TokenizerUpdated;
        }

        private void TokenizerUpdated(ITokenizerResult tokenizerResult, CancellationToken cancellationToken) =>
            Rescan(tokenizerResult, RescanReason.ContentChanged, cancellationToken);

        private void Rescan(ITokenizerResult tokenizerResult, RescanReason rescanReason, CancellationToken cancellationToken)
        {
            if (_resultsRequests.ContainsKey(tokenizerResult.Snapshot))
                return;

            _resultsRequests.Add(tokenizerResult.Snapshot, RunAnalysisAsync(tokenizerResult, rescanReason, cancellationToken));
        }

        private void ForceRescan(ITokenizerResult tokenizerResult, RescanReason rescanReason, CancellationToken cancellationToken)
        {
            _resultsRequests.Remove(tokenizerResult.Snapshot);
            _resultsRequests.Add(tokenizerResult.Snapshot, RunAnalysisAsync(tokenizerResult, rescanReason, cancellationToken));
        }

        private async Task<IAnalysisResult> RunAnalysisAsync(ITokenizerResult tokenizerResult, RescanReason reason, CancellationToken cancellationToken)
        {
            var result = await Task.Run(() => RunParserAsync(tokenizerResult, reason, cancellationToken), cancellationToken).ConfigureAwait(false);
            return result;
        }

        private async Task<IAnalysisResult> RunParserAsync(ITokenizerResult tokenizerResult, RescanReason reason, CancellationToken cancellationToken)
        {
            try
            {
                var parserResult = await _parser.RunAsync(_document, tokenizerResult.Snapshot, tokenizerResult.Tokens, cancellationToken);

                //var includes = parserResult.RootBlock.Tokens
                //    .Where(t => t.Type == RadAsmTokenType.Include)
                //    .Cast<IncludeToken>()
                //    .Select(i => i.Document)
                //    .ToList();
                var includes = new List<IDocument>();
                var analysisResult = new AnalysisResult(_document, parserResult, includes, tokenizerResult.Snapshot);

                CurrentResult = analysisResult;
                AnalysisUpdated?.Invoke(analysisResult, reason, cancellationToken);
                return analysisResult;
            }
            catch (AggregateException /* tokenizer changed but plinq haven't checked CancellationToken yet */)
            {
                throw new OperationCanceledException();
            }
        }
    }
}
