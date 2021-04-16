using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Utilities;
using System;
using System.ComponentModel.Composition;
using VSRAD.Syntax.Core;
using VSRAD.Syntax.Helpers;
using VSRAD.Syntax.Options.Instructions;

namespace VSRAD.Syntax.IntelliSense.SignatureHelp
{
    [Export(typeof(ISignatureHelpSourceProvider))]
    [ContentType(Constants.RadeonAsmSyntaxContentType)]
    [Name(nameof(SignatureHelpSourceProvider))]
    [Order(Before = "Default Signature Help Presenter")]
    internal sealed class SignatureHelpSourceProvider : ISignatureHelpSourceProvider
    {
        private readonly IDocumentFactory _documentFactory;
        private readonly IInstructionListManager _instructionListManager;

        [ImportingConstructor]
        public SignatureHelpSourceProvider(IDocumentFactory documentFactory, IInstructionListManager instructionListManager)
        {
            _documentFactory = documentFactory;
            _instructionListManager = instructionListManager;
        }

        public ISignatureHelpSource TryCreateSignatureHelpSource(ITextBuffer textBuffer)
        {
            if (textBuffer == null) throw new ArgumentNullException(nameof(textBuffer));
            
            var document = _documentFactory.GetOrCreateDocument(textBuffer);
            if (document == null) return null;

            var asmType = textBuffer.CurrentSnapshot.GetAsmType();
            var config = SignatureConfig.GetSignature(asmType);
            return config == null ? null : new SignatureHelpSource(document.DocumentAnalysis, _instructionListManager, textBuffer, config);
        }
    }
}
