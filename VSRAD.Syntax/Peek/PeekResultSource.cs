using VSRAD.Syntax.Parser.Tokens;
using Microsoft.VisualStudio.Imaging.Interop;
using Microsoft.VisualStudio.Language.Intellisense;
using System;
using System.IO;
using System.Threading;

namespace VSRAD.Syntax.Peek
{
    internal sealed class PeekResultSource : IPeekResultSource
    {
        private readonly IPeekResultFactory _peekResultFactory;
        private readonly IBaseToken _token;

        public PeekResultSource(IPeekResultFactory peekResultFactory, IBaseToken token)
        {
            _peekResultFactory = peekResultFactory ?? throw new ArgumentNullException(nameof(peekResultFactory));
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
            var filePath = _token.FilePath;
            var fileName = Path.GetFileName(filePath);
            var startLineIndex = _token.Line.LineNumber;
            var endLineIndex = _token.Line.LineNumber;

            var displayInfo = new PeekResultDisplayInfo2(
                label: string.Format("{0}: {1}-{2} ", fileName, startLineIndex + 1, endLineIndex + 1),
                labelTooltip: _token.FilePath,
                title: fileName,
                titleTooltip: filePath,
                startIndexOfTokenInLabel: 0,
                lengthOfTokenInLabel: 0
            );


            return _peekResultFactory.Create(
                displayInfo,
                default,
                filePath,
                startLineIndex,
                0,
                endLineIndex,
                0,
                0,
                0,
                0,
                0,
                isReadOnly: false
            );
        }
    }
}
