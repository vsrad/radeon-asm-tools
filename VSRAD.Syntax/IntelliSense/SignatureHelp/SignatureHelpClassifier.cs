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
                { RadAsmTokenType.Identifier, typeService.FormalLanguage },
                { RadAsmTokenType.Operation, typeService.Operator },
                { RadAsmTokenType.String, typeService.StringLiteral },
                { RadAsmTokenType.Structural, typeService.FormalLanguage },
                { RadAsmTokenType.Comma, typeService.FormalLanguage },
                { RadAsmTokenType.Semi, typeService.FormalLanguage },
                { RadAsmTokenType.Colon, typeService.FormalLanguage },
                { RadAsmTokenType.Lparen, typeService.FormalLanguage },
                { RadAsmTokenType.Rparen, typeService.FormalLanguage },
                { RadAsmTokenType.LsquareBracket, typeService.FormalLanguage },
                { RadAsmTokenType.RsquareBracket, typeService.FormalLanguage },
                { RadAsmTokenType.LcurveBracket, typeService.FormalLanguage },
                { RadAsmTokenType.RcurveBracket, typeService.FormalLanguage },
                { RadAsmTokenType.Whitespace, typeService.WhiteSpace },
                { RadAsmTokenType.Keyword, typeService.Keyword },
                { RadAsmTokenType.Preprocessor, typeService.PreprocessorKeyword },
                { RadAsmTokenType.Unknown, typeService.Other },
                { RadAsmTokenType.Instruction, registryService.GetClassificationType(RadAsmTokenType.Instruction.GetClassificationTypeName()) },
                { RadAsmTokenType.FunctionName, registryService.GetClassificationType(RadAsmTokenType.FunctionName.GetClassificationTypeName()) },
                { RadAsmTokenType.FunctionReference, registryService.GetClassificationType(RadAsmTokenType.FunctionReference.GetClassificationTypeName()) },
                { RadAsmTokenType.FunctionParameter, registryService.GetClassificationType(RadAsmTokenType.FunctionParameter.GetClassificationTypeName()) },
                { RadAsmTokenType.FunctionParameterReference, registryService.GetClassificationType(RadAsmTokenType.FunctionParameterReference.GetClassificationTypeName()) },
                { RadAsmTokenType.Label, registryService.GetClassificationType(RadAsmTokenType.Label.GetClassificationTypeName()) },
                { RadAsmTokenType.LabelReference, registryService.GetClassificationType(RadAsmTokenType.LabelReference.GetClassificationTypeName()) },
            };
        }

        public event EventHandler<ClassificationChangedEventArgs> ClassificationChanged;
    }
}
