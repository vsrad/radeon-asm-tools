using Microsoft.VisualStudio.Language.Intellisense;
using System;
using System.Threading;
using VSRAD.Syntax.IntelliSense.Navigation;

namespace VSRAD.Syntax.IntelliSense.Peek
{
    internal sealed class PeekResultSource : IPeekResultSource
    {
        private readonly IPeekResultFactory _peekResultFactory;
        private readonly INavigationToken _token;

        public PeekResultSource(IPeekResultFactory peekResultFactory, INavigationToken navigationToken)
        {
            _peekResultFactory = peekResultFactory;
            _token = navigationToken;
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
            var lineNumber = _token.Line.LineNumber;

            const int startLineIndex = 0;
            var endLineIndex = _token.Line.LineText.Length;
            var idIndex = _token.AnalysisToken.Span.End - _token.Line.LineStart;

            var displayInfo = new PeekResultDisplayInfo(
                label: _token.Path,
                labelTooltip: _token.Path,
                title: _token.Path,
                titleTooltip: _token.Path);

            return _peekResultFactory.Create(
                displayInfo,
                _token.Path,
                lineNumber,
                startLineIndex,
                lineNumber,
                endLineIndex,
                idLine: lineNumber,
                idIndex: idIndex
            );
        }
    }
}
