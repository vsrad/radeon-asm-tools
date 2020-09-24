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
            TokenizerUpdated(tokenizer.CurrentResult, CancellationToken.None);
        }

        public async Task<IAnalysisResult> GetAnalysisResultAsync(ITextSnapshot textSnapshot)
        {
            if (_resultsRequests.TryGetValue(textSnapshot, out var task))
                return await task.ConfigureAwait(false);

            throw new NotImplementedException();
        }

        private void TokenizerUpdated(ITokenizerResult tokenizerResult, CancellationToken cancellationToken)
        {
            _resultsRequests.TryAddValue(tokenizerResult.Snapshot,
                () => RunAnalysisAsync(tokenizerResult, cancellationToken));
        }

        private async Task<IAnalysisResult> RunAnalysisAsync(ITokenizerResult tokenizerResult, CancellationToken cancellationToken)
        {
            var result = await Task.Run(() => RunParserAsync(tokenizerResult, cancellationToken), cancellationToken).ConfigureAwait(false);
            return result;
        }

        private async Task<IAnalysisResult> RunParserAsync(ITokenizerResult tokenizerResult, CancellationToken cancellationToken)
        {
            var blocks = await _parser.RunAsync(_document, tokenizerResult.Snapshot, tokenizerResult.Tokens, cancellationToken);
            var rootBlock = blocks[0];

            var includes = rootBlock.Tokens
                .Where(t => t.Type == RadAsmTokenType.Include)
                .Cast<IncludeToken>()
                .Select(i => i.Document)
                .ToList();

            var analysisResult = new AnalysisResult(rootBlock, blocks, includes, tokenizerResult.Snapshot);

            CurrentResult = analysisResult;
            AnalysisUpdated?.Invoke(analysisResult, cancellationToken);
            return analysisResult;
        }
    }
}
