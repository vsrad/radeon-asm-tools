using Microsoft.VisualStudio.Text;
using System;
using System.Threading;
using System.Threading.Tasks;
using Task = System.Threading.Tasks.Task;
using VSRAD.Syntax.Core.Parser;

namespace VSRAD.Syntax.Core
{
    internal class DocumentAnalysis : IDocumentAnalysis
    {
        private readonly IDocument _document;
        private readonly IDocumentTokenizer _tokenizer;
        private readonly IParser _parser;
        private AnalysisRequest _currentRequest;
        private AnalysisRequest _previousRequest;

        private readonly object _updateResultLock = new object();

        public IAnalysisResult CurrentResult { get; private set; }
        public event AnalysisUpdatedEventHandler AnalysisUpdated;

        public DocumentAnalysis(IDocument document, IDocumentTokenizer tokenizer, IParser parser)
        {
            _document = document;
            _tokenizer = tokenizer;
            _parser = parser;

            _tokenizer.TokenizerUpdated += TokenizerUpdated;
            TokenizerUpdated(_tokenizer.CurrentResult, RescanReason.ContentChanged, CancellationToken.None);
        }

        public Task<IAnalysisResult> GetAnalysisResultAsync(ITextSnapshot textSnapshot)
        {
            if (textSnapshot == null)
                throw new ArgumentNullException(nameof(textSnapshot));
            if (textSnapshot.TextBuffer != _document.CurrentSnapshot.TextBuffer)
                throw new ArgumentException("TextSnapshot does not belong to document");
            if (textSnapshot.Version.VersionNumber < _previousRequest.Snapshot?.Version.VersionNumber)
                throw new OperationCanceledException("Old TextSnapshot version requested");

            if (_currentRequest.Snapshot == textSnapshot)
                return _currentRequest.Request;
            if (_previousRequest.Snapshot == textSnapshot)
                return _previousRequest.Request;

            throw new OperationCanceledException("Buffer changes have not yet been processed");
        }

        private void TokenizerUpdated(ITokenizerResult tokenizerResult, RescanReason reason,
            CancellationToken cancellationToken)
        {
            var analysisResultTask = RunAnalysisAsync(tokenizerResult, reason, cancellationToken);
            _previousRequest = _currentRequest;
            _currentRequest = new AnalysisRequest()
            {
                Snapshot = tokenizerResult.Snapshot,
                Request = analysisResultTask,
            };
        }

        private Task<IAnalysisResult> RunAnalysisAsync(ITokenizerResult tokenizerResult, RescanReason reason,
            CancellationToken cancellationToken) =>
            Task.Run(() => RunParserAsync(tokenizerResult, reason, cancellationToken), cancellationToken);

        private async Task<IAnalysisResult> RunParserAsync(ITokenizerResult tokenizerResult, RescanReason reason, CancellationToken cancellationToken)
        {
            var parserResult = await _parser.RunAsync(_document, tokenizerResult.Snapshot, tokenizerResult.Tokens, cancellationToken);
            var analysisResult = new AnalysisResult(parserResult, tokenizerResult.Snapshot);

            cancellationToken.ThrowIfCancellationRequested();
            SynchronousUpdate(analysisResult, reason, cancellationToken);
            
            return analysisResult;
        }

        private void SynchronousUpdate(IAnalysisResult analysisResult, RescanReason reason, 
            CancellationToken cancellationToken)
        {
            lock (_updateResultLock)
            {
                CurrentResult = analysisResult;
                AnalysisUpdated?.Invoke(analysisResult, reason, cancellationToken);
            }
        }

        public void Dispose()
        {
            _tokenizer.TokenizerUpdated -= TokenizerUpdated;
        }
    }

    internal struct AnalysisRequest
    {
        public ITextSnapshot Snapshot;
        public Task<IAnalysisResult> Request;
    }
}
