using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Utilities;
using System;
using System.ComponentModel.Composition;

namespace VSRAD.Syntax.IntelliSense.QuickInfo
{
    [Export(typeof(IAsyncQuickInfoSourceProvider))]
    [ContentType(Constants.RadeonAsmSyntaxContentType)]
    [Name(nameof(QuickInfoSourceProvider))]
    [Order]
    internal class QuickInfoSourceProvider : IAsyncQuickInfoSourceProvider
    {
        private readonly Lazy<INavigationTokenService> _navigationService;
        private readonly Lazy<IIntellisenseDescriptionBuilder> _descriptionBuilder;

        [ImportingConstructor]
        public QuickInfoSourceProvider(Lazy<INavigationTokenService> navigationService,
            Lazy<IIntellisenseDescriptionBuilder> descriptionBuilder)
        {
            _navigationService = navigationService;
            _descriptionBuilder = descriptionBuilder;
        }

        public IAsyncQuickInfoSource TryCreateQuickInfoSource(ITextBuffer textBuffer)
        {
            if (textBuffer == null)
                throw new ArgumentNullException(nameof(textBuffer));

            return textBuffer.Properties.GetOrCreateSingletonProperty(() => 
                new QuickInfoSource(textBuffer, _navigationService.Value, _descriptionBuilder.Value) as IAsyncQuickInfoSource);
        }
    }
}
