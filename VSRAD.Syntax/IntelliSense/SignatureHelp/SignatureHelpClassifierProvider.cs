using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Language.StandardClassification;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Utilities;

namespace VSRAD.Syntax.IntelliSense.SignatureHelp
{
    [Export(typeof(IClassifierProvider))]
    [ContentType(Constants.RadeonAsm1SyntaxSignatureHelpContentType)]
    [ContentType(Constants.RadeonAsm2SyntaxSignatureHelpContentType)]
    internal class SignatureHelpClassifierProvider : IClassifierProvider
    {
        [ImportingConstructor]
        public SignatureHelpClassifierProvider(IStandardClassificationService typeService, IClassificationTypeRegistryService registryService)
        {
            SignatureHelpClassifier.InitializeClassifierDictionary(typeService, registryService);
        }

        public IClassifier GetClassifier(ITextBuffer textBuffer)
        {
            return textBuffer.Properties.GetOrCreateSingletonProperty<IClassifier>(
                () => new SignatureHelpClassifier(textBuffer));
        }
    }
}
