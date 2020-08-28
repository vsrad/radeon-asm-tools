using Microsoft.VisualStudio.Text;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VSRAD.Syntax.Helpers;
using VSRAD.Syntax.Core.Tokens;

namespace VSRAD.Syntax.Core
{
    internal class DocumentAnalysis : IDocumentAnalysis
    {
        private readonly IParser _parser;
        private readonly ConcurrentDictionary<int, IAnalysisResult> _results;
        private CancellationTokenSource _cts;

        public event AnalysisUpdatedEventHandler AnalysisUpdated;

        public DocumentAnalysis(IDocumentTokenizer tokenizer, IParser parser)
        {
            _parser = parser;
            _cts = new CancellationTokenSource();

            tokenizer.TokenizerUpdated += TokenizerUpdated;
        }

        public async Task<IAnalysisResult> GetAnalysisResultAsync(ITextSnapshot textSnapshot)
        {
            if (_results.TryGetValue(textSnapshot.Version.VersionNumber, out var analysisResult))
                return analysisResult;

            throw new NotImplementedException();
        }

        private void TokenizerUpdated(ITextSnapshot snapshot, IEnumerable<TrackingToken> tokens)
        {
            _cts.Cancel();
            _cts = new CancellationTokenSource();
            RunAnalysisAsync(snapshot, tokens, _cts.Token).RunAsyncWithoutAwait();
        }

        private Task<IAnalysisResult> RunAnalysisAsync(ITextSnapshot snapshot, IEnumerable<TrackingToken> tokens, CancellationToken cancellationToken) 
            => Task.Run(() =>
            {
                var analysisResult = _results.GetOrAdd(snapshot.Version.VersionNumber, (version) =>
                {
                    var blocks = _parser.Run(tokens, snapshot, cancellationToken);
                    var rootBlock = blocks[0];

                    var includes = rootBlock.Tokens
                        .Where(t => t.Type == RadAsmTokenType.Include)
                        .Cast<IncludeToken>()
                        .Select(i => i.Document)
                        .ToList();

                    return new AnalysisResult(blocks, includes, snapshot);
                });

                return analysisResult;
            }, cancellationToken);
    }
}
