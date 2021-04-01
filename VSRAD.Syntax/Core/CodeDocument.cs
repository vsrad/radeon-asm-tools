using System.Threading;
using Microsoft.VisualStudio.Text;
using VSRAD.Syntax.Core.Lexer;
using VSRAD.Syntax.Core.Parser;
using VSRAD.Syntax.Options.Instructions;

namespace VSRAD.Syntax.Core
{
    internal class CodeDocument : Document
    {
        private readonly IInstructionListManager _instructionListManager;

        public CodeDocument(ITextDocumentFactoryService textDocumentFactory,
            IInstructionListManager instructionListManager,
            ITextDocument textDocument,
            ILexer lexer, IParser parser, OnDestroyAction onDestroy)
            : base(textDocumentFactory, textDocument, lexer, parser, onDestroy)
        {
            _instructionListManager = instructionListManager;
            _instructionListManager.InstructionsUpdated += InstructionsUpdated;
        }

        private void InstructionsUpdated(IInstructionListManager sender, Helpers.AsmType asmType) =>
            DocumentAnalysis.Rescan(RescanReason.InstructionsChanged, CancellationToken.None);

        public override void Dispose()
        {
            if (IsDisposed) return;

            base.Dispose();
            _instructionListManager.InstructionsUpdated -= InstructionsUpdated;
        }
    }
}
