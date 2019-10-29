using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Utilities;
using System;
using System.ComponentModel.Composition;

namespace VSRAD.Syntax.Peek
{
    [Export(typeof(IPeekableItemSourceProvider))]
    [ContentType(Constants.RadeonAsmSyntaxContentType)]
    [Name(nameof(PeekableItemSourceProvider))]
    [SupportsStandaloneFiles(true)]
    [SupportsPeekRelationship("IsDefinedBy")]
    internal sealed class PeekableItemSourceProvider : IPeekableItemSourceProvider
    {
        private readonly IPeekResultFactory _peekResultFactory;
        private readonly DefinitionService.DefinitionService _definitionService;

        [ImportingConstructor]
        public PeekableItemSourceProvider(
            IPeekResultFactory peekResultFactory,
            DefinitionService.DefinitionService definitionService)
        {
            _peekResultFactory = peekResultFactory;
            _definitionService = definitionService;
        }

        public IPeekableItemSource TryCreatePeekableItemSource(ITextBuffer textBuffer)
        {
            if (textBuffer == null)
                throw new ArgumentNullException(nameof(textBuffer));

            return textBuffer.Properties.GetOrCreateSingletonProperty(() => new PeekableItemSource(_peekResultFactory, _definitionService));
        }
    }
}