using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Utilities;

namespace VSRAD.Syntax.SyntaxHighlighter
{
    [Export(typeof(IClassifierProvider))]
    [ContentType(Constants.RadeonAsmSyntaxContentType)]
    internal class ClassifierProvider : IClassifierProvider
    {
#pragma warning disable 649

        [Import]
        private readonly IClassificationTypeRegistryService classificationRegistry = null;

        public IClassifier GetClassifier(ITextBuffer buffer)
        {
            var asm1Classifier = buffer.Properties.GetOrCreateSingletonProperty(() => new Asm1Classifier(classificationRegistry, buffer));
            return asm1Classifier;
        }
#pragma warning restore 649
    }

    [Export(typeof(IClassifierProvider))]
    [ContentType(Constants.RadeonAsm2SyntaxContentType)]
    internal class AsmClassifierProvider : IClassifierProvider
    {
#pragma warning disable 649

        [Import]
        private readonly IClassificationTypeRegistryService classificationRegistry = null;

        public IClassifier GetClassifier(ITextBuffer buffer)
        {
            var asmClassifier = buffer.Properties.GetOrCreateSingletonProperty(() => new Asm2Classifier(classificationRegistry, buffer));
            return asmClassifier;
        }
#pragma warning restore 649
    }
}