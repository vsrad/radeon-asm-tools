using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Utilities;
using System.ComponentModel.Composition;

namespace VSRAD.Syntax.SyntaxHighlighter
{
    internal static class ClassifierClassificationDefinition
    {
        // This disables "The field is never used" compiler's warning. Justification: the field is used by MEF.
#pragma warning disable 169

        #region Content Type and File Extension Definitions

        [Export]
        [Name(Constants.RadeonAsmSyntaxContentType)]
        [BaseDefinition(Constants.RadeonAsmSyntaxBaseContentType)]
        internal static ContentTypeDefinition contentTypeDefinition = null;

        [Export]
        [Name(Constants.RadeonAsm2SyntaxContentType)]
        [BaseDefinition(Constants.RadeonAsmSyntaxContentType)]
        internal static ContentTypeDefinition asm2ContentTypeDefinition = null;

        [Export]
        [Name(Constants.RadeonAsmDocumentationContentType)]
        [BaseDefinition(Constants.RadeonAsmSyntaxContentType)]
        internal static ContentTypeDefinition asmDocContentTypeDefinition = null;

        [Export]
        [FileExtension(Constants.FileExtensionAsm2)]
        [ContentType(Constants.RadeonAsm2SyntaxContentType)]
        internal static FileExtensionToContentTypeDefinition asm2FileExtensionDefinition = null;

        [Export]
        [FileExtension(Constants.FileExtensionAsm1)]
        [ContentType(Constants.RadeonAsmSyntaxContentType)]
        internal static FileExtensionToContentTypeDefinition asm1FileExtensionDefinition = null;

        [Export]
        [FileExtension(Constants.FileExtensionS)]
        [ContentType(Constants.RadeonAsmSyntaxContentType)]
        internal static FileExtensionToContentTypeDefinition sFileExtensionDefinition = null;

        [Export]
        [FileExtension(Constants.FileExtensionInc)]
        [ContentType(Constants.RadeonAsmSyntaxContentType)]
        internal static FileExtensionToContentTypeDefinition incFileExtensionDefinition = null;

        [Export]
        [FileExtension(Constants.FileExtensionAsm1Doc)]
        [ContentType(Constants.RadeonAsmDocumentationContentType)]
        internal static FileExtensionToContentTypeDefinition docAsm1FileExtensionDefinition = null;

        [Export]
        [FileExtension(Constants.FileExtensionAsm2Doc)]
        [ContentType(Constants.RadeonAsmDocumentationContentType)]
        internal static FileExtensionToContentTypeDefinition docAsm2FileExtensionDefinition = null;

        #endregion

        #region Classification Type Definitions

        [Export]
        [Name(PredefinedClassificationTypeNames.Instructions)]
        [BaseDefinition(Constants.RadeonAsmSyntaxContentType)]
        internal static ClassificationTypeDefinition insrtuctionsDefinition = null;

        [Export]
        [Name(PredefinedClassificationTypeNames.Arguments)]
        [BaseDefinition(Constants.RadeonAsmSyntaxContentType)]
        internal static ClassificationTypeDefinition argumentsDefinition = null;

        [Export]
        [Name(PredefinedClassificationTypeNames.Functions)]
        [BaseDefinition(Constants.RadeonAsmSyntaxContentType)]
        internal static ClassificationTypeDefinition functionsDefinition = null;

        [Export]
        [Name(PredefinedClassificationTypeNames.Labels)]
        [BaseDefinition(Constants.RadeonAsmSyntaxContentType)]
        internal static ClassificationTypeDefinition labesDefinition = null;

        #endregion

        #region Classification Format Productions

        internal class BaseClassificationFormatDefinition : ClassificationFormatDefinition
        {
            public BaseClassificationFormatDefinition(ThemeColorManager colorManager, string type, string name)
            {
                DisplayName = name;
                var colors = colorManager.GetDefaultColors(type);
                ForegroundColor = colors.Foreground;
                BackgroundColor = colors.Background;
            }
        }

        internal class BaseMarkerFormatDefinition : MarkerFormatDefinition
        {
            public BaseMarkerFormatDefinition(ThemeColorManager colorManager, string name)
            {
                DisplayName = name;
                var colors = colorManager.GetDefaultColors(name);
                ForegroundColor = colors.Foreground;
                BackgroundColor = colors.Background;
            }
        }

        [Export(typeof(EditorFormatDefinition))]
        [ClassificationType(ClassificationTypeNames = PredefinedClassificationTypeNames.Instructions)]
        [Name(PredefinedClassificationTypeNames.Instructions)]
        [UserVisible(true)]
        internal sealed class InstructionFormat : BaseClassificationFormatDefinition
        {
            [ImportingConstructor]
            public InstructionFormat(ThemeColorManager colorManager) 
                : base(colorManager, PredefinedClassificationTypeNames.Instructions, PredefinedClassificationFormatNames.Instructions) { }
        }

        [Export(typeof(EditorFormatDefinition))]
        [ClassificationType(ClassificationTypeNames = PredefinedClassificationTypeNames.Arguments)]
        [Name(PredefinedClassificationTypeNames.Arguments)]
        [UserVisible(true)]
        internal sealed class ArgumentFormat : BaseClassificationFormatDefinition
        {
            [ImportingConstructor]
            public ArgumentFormat(ThemeColorManager colorManager)
                : base(colorManager, PredefinedClassificationTypeNames.Arguments, PredefinedClassificationFormatNames.Arguments) { }
        }

        [Export(typeof(EditorFormatDefinition))]
        [ClassificationType(ClassificationTypeNames = PredefinedClassificationTypeNames.Functions)]
        [Name(PredefinedClassificationTypeNames.Functions)]
        [UserVisible(true)]
        internal sealed class FunctionsFormat : BaseClassificationFormatDefinition
        {
            [ImportingConstructor]
            public FunctionsFormat(ThemeColorManager colorManager)
                : base(colorManager, PredefinedClassificationTypeNames.Functions, PredefinedClassificationFormatNames.Functions) { }
        }

        [Export(typeof(EditorFormatDefinition))]
        [ClassificationType(ClassificationTypeNames = PredefinedClassificationTypeNames.Labels)]
        [Name(PredefinedClassificationTypeNames.Labels)]
        [UserVisible(true)]
        internal sealed class LabelFormat : BaseClassificationFormatDefinition
        {
            [ImportingConstructor]
            public LabelFormat(ThemeColorManager colorManager)
                : base(colorManager, PredefinedClassificationTypeNames.Labels, PredefinedClassificationFormatNames.Labels) { }
        }

        [Export(typeof(EditorFormatDefinition))]
        [Name(PredefinedMarkerFormatNames.ReferenceIdentifier)]
        [UserVisible(true)]
        internal sealed class ReferenceIdentifierHighlightFormat : BaseMarkerFormatDefinition
        {
            [ImportingConstructor]
            public ReferenceIdentifierHighlightFormat(ThemeColorManager colorManager)
                : base(colorManager, PredefinedMarkerFormatNames.ReferenceIdentifier) { }
        }

        [Export(typeof(EditorFormatDefinition))]
        [Name(PredefinedMarkerFormatNames.DefinitionIdentifier)]
        [UserVisible(true)]
        internal sealed class DefinitionIdentifierHighlightFormat : BaseMarkerFormatDefinition
        {
            [ImportingConstructor]
            public DefinitionIdentifierHighlightFormat(ThemeColorManager colorManager)
                : base(colorManager, PredefinedMarkerFormatNames.DefinitionIdentifier) { }
        }

        [Export(typeof(EditorFormatDefinition))]
        [Name(PredefinedMarkerFormatNames.BraceMatching)]
        [UserVisible(true)]
        internal sealed class BraceMatchingHighlightFormat : BaseMarkerFormatDefinition
        {
            [ImportingConstructor]
            public BraceMatchingHighlightFormat(ThemeColorManager colorManager)
                : base(colorManager, PredefinedMarkerFormatNames.BraceMatching) { }
        }
        #endregion

#pragma warning restore 169
    }
}
