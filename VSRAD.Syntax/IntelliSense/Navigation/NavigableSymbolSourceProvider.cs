using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;
using System.ComponentModel.Composition;

namespace VSRAD.Syntax.IntelliSense.Navigation
{
    [Export(typeof(INavigableSymbolSourceProvider))]
    [ContentType(Constants.RadeonAsmSyntaxContentType)]
    [Name(nameof(NavigableSymbolSourceProvider))]
    internal sealed class NavigableSymbolSourceProvider : INavigableSymbolSourceProvider
    {
        private readonly INavigationTokenService _navigationService;

        [ImportingConstructor]
        public NavigableSymbolSourceProvider(INavigationTokenService navigationService)
        {
            _navigationService = navigationService;
        }

        public INavigableSymbolSource TryCreateNavigableSymbolSource(ITextView textView, ITextBuffer buffer) =>
            buffer.Properties.GetOrCreateSingletonProperty(() => new NavigableSymbolSource(_navigationService));
    }
}
