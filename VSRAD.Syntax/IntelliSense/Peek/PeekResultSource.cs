using VSRAD.Syntax.Core.Tokens;
using Microsoft.VisualStudio.Language.Intellisense;
using System;
using System.Threading;
using Microsoft.VisualStudio.Text;

namespace VSRAD.Syntax.IntelliSense.Peek
{
    internal sealed class PeekResultSource : IPeekResultSource
    {
        private readonly IPeekResultFactory _peekResultFactory;
        private readonly ITextDocumentFactoryService _textDocumentFactory;
        private readonly ITextSnapshot _version;
        private readonly AnalysisToken _token;

        public PeekResultSource(IPeekResultFactory peekResultFactory,
            ITextDocumentFactoryService textDocumentFactory,
            ITextSnapshot version,
            AnalysisToken token)
        {
            _peekResultFactory = peekResultFactory;
            _textDocumentFactory = textDocumentFactory;
            _version = version;
            _token = token;
        }

        public void FindResults(string relationshipName, IPeekResultCollection resultCollection, CancellationToken cancellationToken, IFindPeekResultsCallback callback)
        {
            if (resultCollection == null)
                throw new ArgumentNullException(nameof(resultCollection));

            if (!string.Equals(relationshipName, PredefinedPeekRelationships.Definitions.Name, StringComparison.OrdinalIgnoreCase))
                return;

            resultCollection.Add(CreateResult());
        }

        private IDocumentPeekResult CreateResult()
        {
            if (!_textDocumentFactory.TryGetTextDocument(_version.TextBuffer, out var document))
                return null;

            var endPosition = _token.TrackingToken.GetEnd(_version);
            var line = _version.GetLineFromPosition(endPosition);

            var startLineIndex = line.LineNumber;
            var startIndex = endPosition - line.Start.Position;
            var endLineIndex = line.LineNumber;
            var endIndex = endPosition - line.Start.Position;

            var displayInfo = new PeekResultDisplayInfo2(
                label: string.Format("{0}: {1}-{2} ", document.FilePath, startLineIndex + 1, endLineIndex + 1),
                labelTooltip: document.FilePath,
                title: document.FilePath,
                titleTooltip: document.FilePath,
                startIndexOfTokenInLabel: 0,
                lengthOfTokenInLabel: 0
            );

            return _peekResultFactory.Create(
                displayInfo,
                default,
                document.FilePath,
                startLineIndex,
                startIndex,
                endLineIndex,
                endIndex,
                0,
                0,
                0,
                0,
                isReadOnly: false
            );
        }
    }
}
