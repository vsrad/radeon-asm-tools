using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Utilities;
using System;
using System.ComponentModel.Composition;

namespace VSRAD.Syntax.IntelliSense.Peek
{
    [Export(typeof(IPeekableItemSourceProvider))]
    [ContentType(Constants.RadeonAsmSyntaxContentType)]
    [Name(nameof(PeekableItemSourceProvider))]
    [SupportsStandaloneFiles(true)]
    [SupportsPeekRelationship("IsDefinedBy")]
    internal sealed class PeekableItemSourceProvider : IPeekableItemSourceProvider
    {
        private readonly INavigationTokenService _navigationTokenService;
        private readonly IPeekResultFactory _peekResultFactory;

        [ImportingConstructor]
        public PeekableItemSourceProvider(IPeekResultFactory peekResultFactory,
            INavigationTokenService definitionService)
        {
            _peekResultFactory = peekResultFactory;
            _navigationTokenService = definitionService;
        }

        public IPeekableItemSource TryCreatePeekableItemSource(ITextBuffer textBuffer)
        {
            if (textBuffer == null)
                throw new ArgumentNullException(nameof(textBuffer));

            return new PeekableItemSource(textBuffer, _peekResultFactory, _navigationTokenService);
        }
    }
}