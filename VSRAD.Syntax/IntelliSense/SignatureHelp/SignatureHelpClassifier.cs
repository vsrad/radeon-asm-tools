using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Language.StandardClassification;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using VSRAD.Syntax.Core.Tokens;

namespace VSRAD.Syntax.IntelliSense.SignatureHelp
{
    internal class SignatureHelpClassifier : IClassifier
    {
        private static Dictionary<RadAsmTokenType, IClassificationType> _tokenClassification;
        private readonly ITextBuffer _textBuffer;

        public SignatureHelpClassifier(ITextBuffer textBuffer)
        {
            _textBuffer = textBuffer;
        }

        public IList<ClassificationSpan> GetClassificationSpans(SnapshotSpan span)
        {
            if (_textBuffer.Properties.TryGetProperty<ISignatureHelpSession>(typeof(ISignatureHelpSession), out var session))
            {
                if (session.SelectedSignature is ISyntaxSignature signature)
                {
                    var content = signature.Content;
                    if (content == _textBuffer.CurrentSnapshot.GetText())
                    {
                        var snapshot = _textBuffer.CurrentSnapshot;
                        var classifications = new List<ClassificationSpan>();

                        foreach (var displayPart in signature.DisplayParts)
                        {
                            var snapshotSpan = new SnapshotSpan(snapshot, displayPart.Span);
                            classifications.Add(new ClassificationSpan(snapshotSpan,
                                _tokenClassification[displayPart.Type]));
                        }

                        return classifications;
                    }
                }
            }

            return new List<ClassificationSpan>();
        }

        public static void InitializeClassifierDictionary(IStandardClassificationService typeService, IClassificationTypeRegistryService registryService)
        {
            if (_tokenClassification != null)
                return;

            _tokenClassification = new Dictionary<RadAsmTokenType, IClassificationType>()
            {
                { RadAsmTokenType.Comment, typeService.Comment },
                { RadAsmTokenType.Number, typeService.NumberLiteral },
                { RadAsmTokenType.Structural, typeService.FormalLanguage },
                { RadAsmTokenType.Instruction, registryService.GetClassificationType(RadAsmTokenType.Instruction.GetClassificationTypeName()) },
                { RadAsmTokenType.FunctionName, registryService.GetClassificationType(RadAsmTokenType.FunctionName.GetClassificationTypeName()) },
                { RadAsmTokenType.FunctionParameter, registryService.GetClassificationType(RadAsmTokenType.FunctionParameter.GetClassificationTypeName()) },
                { RadAsmTokenType.GlobalVariableReference, registryService.GetClassificationType(RadAsmTokenType.FunctionParameter.GetClassificationTypeName()) },
            };
        }

        public event EventHandler<ClassificationChangedEventArgs> ClassificationChanged;
    }
}
