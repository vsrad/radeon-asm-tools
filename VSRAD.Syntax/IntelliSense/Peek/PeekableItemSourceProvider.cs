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
        private readonly RadeonServiceProvider _serviceProvider;
        private readonly INavigationTokenService _navigationTokenService;

        [ImportingConstructor]
        public PeekableItemSourceProvider(
            RadeonServiceProvider serviceProvider,
            INavigationTokenService definitionService)
        {
            _serviceProvider = serviceProvider;
            _navigationTokenService = definitionService;
        }

        public IPeekableItemSource TryCreatePeekableItemSource(ITextBuffer textBuffer)
        {
            if (textBuffer == null)
                throw new ArgumentNullException(nameof(textBuffer));

            return textBuffer.Properties.GetOrCreateSingletonProperty(() => 
                new PeekableItemSource(textBuffer, _serviceProvider.PeekResultFactory, _navigationTokenService) as IPeekableItemSource);
        }
    }
}