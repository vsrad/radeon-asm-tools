using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;
using System;
using System.ComponentModel.Composition;

namespace VSRAD.Syntax.IntelliSense.Navigation
{
    [Export(typeof(INavigableSymbolSourceProvider))]
    [ContentType(Constants.RadeonAsmSyntaxContentType)]
    [Name(nameof(NavigableSymbolSourceProvider))]
    internal sealed class NavigableSymbolSourceProvider : INavigableSymbolSourceProvider
    {
        private readonly IIntelliSenseService _intelliSenseService;

        [ImportingConstructor]
        public NavigableSymbolSourceProvider(IIntelliSenseService intelliSenseService)
        {
            _intelliSenseService = intelliSenseService;
        }

        public INavigableSymbolSource TryCreateNavigableSymbolSource(ITextView textView, ITextBuffer textBuffer)
        {
            if (textBuffer == null)
                throw new ArgumentNullException(nameof(textBuffer));

            return textBuffer.Properties.GetOrCreateSingletonProperty(() =>
                new NavigableSymbolSource(_intelliSenseService));
        }
    }
}
