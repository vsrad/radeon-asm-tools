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

        public IAnalysisResult CurrentResult { get; private set; }
        public event AnalysisUpdatedEventHandler AnalysisUpdated;

        public DocumentAnalysis(IDocument document, IDocumentTokenizer tokenizer, IParser parser)
        {
            _document = document;
            _parser = parser;
            _resultsRequests = new FixedSizeDictionary<ITextSnapshot, Task<IAnalysisResult>>(100);

            tokenizer.TokenizerUpdated += TokenizerUpdated;
            TokenizerUpdated(tokenizer.CurrentResult, RescanReason.ContentChanged, CancellationToken.None);
        }

        public async Task<IAnalysisResult> GetAnalysisResultAsync(ITextSnapshot textSnapshot)
        {
            if (_resultsRequests.TryGetValue(textSnapshot, out var task))
                return await task.ConfigureAwait(false);

            throw new NotImplementedException();
        }

        private void TokenizerUpdated(ITokenizerResult tokenizerResult, RescanReason reason, CancellationToken cancellationToken)
        {
            _resultsRequests.AddValue(tokenizerResult.Snapshot,
                () => RunAnalysisAsync(tokenizerResult, reason, cancellationToken));
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

                var includes = parserResult.RootBlock.Tokens
                    .Where(t => t.Type == RadAsmTokenType.Include)
                    .Cast<IncludeToken>()
                    .Select(i => i.Document)
                    .ToList();

                var analysisResult = new AnalysisResult(parserResult, includes, tokenizerResult.Snapshot);

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
