using Microsoft.VisualStudio.Text;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VSRAD.Syntax.Helpers;
using VSRAD.Syntax.Core.Tokens;
using Microsoft.VisualStudio.Threading;
using Microsoft.VisualStudio.Shell;
using Task = System.Threading.Tasks.Task;

namespace VSRAD.Syntax.Core
{
    internal class DocumentAnalysis : IDocumentAnalysis
    {
        private readonly IParser _parser;
        private readonly ConcurrentDictionary<ITextSnapshot, AsyncLazy<IAnalysisResult>> _resultsRequests;
        private readonly DocumentSnapshotComparer _comparer;
        private CancellationTokenSource _cts;

        public event AnalysisUpdatedEventHandler AnalysisUpdated;

        public DocumentAnalysis(IDocumentTokenizer tokenizer, IParser parser)
        {
            _parser = parser;
            _cts = new CancellationTokenSource();

            _comparer = new DocumentSnapshotComparer();
            _resultsRequests = new ConcurrentDictionary<ITextSnapshot, AsyncLazy<IAnalysisResult>>(_comparer);

            tokenizer.TokenizerUpdated += TokenizerUpdated;
            TokenizerUpdated(tokenizer.CurrentResult);
        }

        public async Task<IAnalysisResult> GetAnalysisResultAsync(ITextSnapshot textSnapshot)
        {
            if (_resultsRequests.TryGetValue(textSnapshot, out var asyncLazy))
                return await asyncLazy.GetValueAsync();

            throw new NotImplementedException();
        }

        private void TokenizerUpdated(TokenizerResult tokenizerResult)
        {
            _cts.Cancel();
            _cts = new CancellationTokenSource();
            RunAnalysisAsync(tokenizerResult, _cts.Token).RunAsyncWithoutAwait();
        }

        private async Task RunAnalysisAsync(TokenizerResult tokenizerResult, CancellationToken cancellationToken)
        {
            var analysisResult = await _resultsRequests
                .GetOrAdd(
                    tokenizerResult.Snapshot, 
                    version => new AsyncLazy<IAnalysisResult>(() =>
                        Task.Run(async () => await RunParserAsync(tokenizerResult, cancellationToken)),
                        ThreadHelper.JoinableTaskFactory))
                .GetValueAsync();

            AnalysisUpdated?.Invoke(analysisResult);
        }

        private async Task<IAnalysisResult> RunParserAsync(TokenizerResult tokenizerResult, CancellationToken cancellationToken)
        {
            var blocks = await _parser.RunAsync(tokenizerResult.Tokens, tokenizerResult.Snapshot, cancellationToken);
            var rootBlock = blocks[0];

            var includes = rootBlock.Tokens
                .Where(t => t.Type == RadAsmTokenType.Include)
                .Cast<IncludeToken>()
                .Select(i => i.Document)
                .ToList();

            return new AnalysisResult(blocks, includes, tokenizerResult.Snapshot);
        }

        private class DocumentSnapshotComparer : IEqualityComparer<ITextSnapshot>
        {
            public bool Equals(ITextSnapshot x, ITextSnapshot y) =>
                x.Version.VersionNumber == y.Version.VersionNumber;

            public int GetHashCode(ITextSnapshot obj) => obj.GetHashCode();
        }
    }
}
