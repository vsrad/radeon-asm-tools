using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Utilities;
using VSRAD.Syntax.Options;

namespace VSRAD.Syntax.SyntaxHighlighter
{
//    [Export(typeof(IClassifierProvider))]
//    [ContentType(Constants.RadeonAsmSyntaxContentType)]
//    internal class Asm1ClassifierProvider : IClassifierProvider
//    {
//#pragma warning disable 649

//        private readonly IClassificationTypeRegistryService _classificationRegistry;
//        private readonly InstructionListManager _instructionListManager;

//        [ImportingConstructor]
//        public Asm1ClassifierProvider(IClassificationTypeRegistryService classificationRegistry, InstructionListManager instructionListManager)
//        {
//            _classificationRegistry = classificationRegistry;
//            _instructionListManager = instructionListManager;
//        }


//        public IClassifier GetClassifier(ITextBuffer buffer)
//        {
//            var asm1Classifier = buffer.Properties.GetOrCreateSingletonProperty(() => new Asm1Classifier(_classificationRegistry, buffer, _instructionListManager));
//            return asm1Classifier;
//        }
//#pragma warning restore 649
//    }

//    [Export(typeof(IClassifierProvider))]
//    [ContentType(Constants.RadeonAsm2SyntaxContentType)]
//    internal class Asm2ClassifierProvider : IClassifierProvider
//    {
//#pragma warning disable 649

//        private readonly IClassificationTypeRegistryService _classificationRegistry;
//        private readonly InstructionListManager _instructionListManager;

//        [ImportingConstructor]
//        public Asm2ClassifierProvider(IClassificationTypeRegistryService classificationRegistry, InstructionListManager instructionListManager)
//        {
//            _classificationRegistry = classificationRegistry;
//            _instructionListManager = instructionListManager;
//        }

//        public IClassifier GetClassifier(ITextBuffer buffer)
//        {
//            var asmClassifier = buffer.Properties.GetOrCreateSingletonProperty(() => new Asm2Classifier(_classificationRegistry, buffer, _instructionListManager));
//            return asmClassifier;
//        }
//#pragma warning restore 649
//    }
}