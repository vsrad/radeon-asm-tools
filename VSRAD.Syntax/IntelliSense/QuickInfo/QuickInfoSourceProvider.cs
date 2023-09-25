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
        private readonly Lazy<IIntelliSenseService> _intelliSenseService;
        private readonly Lazy<IIntelliSenseDescriptionBuilder> _descriptionBuilder;

        [ImportingConstructor]
        public QuickInfoSourceProvider(Lazy<IIntelliSenseService> intelliSenseService,
            Lazy<IIntelliSenseDescriptionBuilder> descriptionBuilder)
        {
            _intelliSenseService = intelliSenseService;
            _descriptionBuilder = descriptionBuilder;
        }

        public IAsyncQuickInfoSource TryCreateQuickInfoSource(ITextBuffer textBuffer)
        {
            if (textBuffer == null)
                throw new ArgumentNullException(nameof(textBuffer));

            return textBuffer.Properties.GetOrCreateSingletonProperty(() =>
                new QuickInfoSource(textBuffer, _intelliSenseService.Value, _descriptionBuilder.Value));
        }
    }
}
