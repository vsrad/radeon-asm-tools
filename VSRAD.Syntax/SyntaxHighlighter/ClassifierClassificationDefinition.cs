using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Utilities;
using System.ComponentModel.Composition;
using System.Windows.Media;

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

        [Export(typeof(EditorFormatDefinition))]
        [ClassificationType(ClassificationTypeNames = Constants.RadeonAsmSyntaxContentType)]
        [Name(PredefinedClassificationFormatNames.Instructions)]
        [UserVisible(true)]
        internal sealed class InstructionFormat : ClassificationFormatDefinition
        {
            [Import]
            internal IClassificationTypeRegistryService ClassificationRegistry = null;

            public InstructionFormat()
            {
                this.ForegroundColor = Colors.Purple;
            }
        }

        [Export(typeof(EditorFormatDefinition))]
        [ClassificationType(ClassificationTypeNames = PredefinedClassificationTypeNames.Arguments)]
        [Name(PredefinedClassificationFormatNames.Arguments)]
        [UserVisible(true)]
        internal sealed class ArgumentFormat : ClassificationFormatDefinition
        {
            public ArgumentFormat()
            {
                ForegroundColor = Color.FromArgb(255, 110, 110, 110);
            }
        }

        [Export(typeof(EditorFormatDefinition))]
        [ClassificationType(ClassificationTypeNames = PredefinedClassificationTypeNames.Functions)]
        [Name(PredefinedClassificationFormatNames.Functions)]
        [UserVisible(true)]
        internal sealed class FunctionsFormat : ClassificationFormatDefinition
        {
            public FunctionsFormat()
            {
                ForegroundColor = Colors.Teal;
            }
        }

        [Export(typeof(EditorFormatDefinition))]
        [ClassificationType(ClassificationTypeNames = PredefinedClassificationTypeNames.Labels)]
        [Name(PredefinedClassificationFormatNames.Labels)]
        [UserVisible(true)]
        internal sealed class LabelFormat : ClassificationFormatDefinition
        {
            public LabelFormat()
            {
                ForegroundColor = Colors.Olive;
            }
        }

        [Export(typeof(EditorFormatDefinition))]
        [Name(PredefinedMarkerFormatNames.ReferenceIdentifierLight)]
        [UserVisible(true)]
        internal sealed class ReferenceIdentifierHighlightFormatLight : MarkerFormatDefinition
        {
            public ReferenceIdentifierHighlightFormatLight()
            {
                BackgroundColor = Color.FromArgb(255, 219, 224, 204);
            }
        }

        [Export(typeof(EditorFormatDefinition))]
        [Name(PredefinedMarkerFormatNames.ReferenceIdentifierDark)]
        [UserVisible(true)]
        internal sealed class ReferenceIdentifierHighlightFormatDark : MarkerFormatDefinition
        {
            public ReferenceIdentifierHighlightFormatDark()
            {
                BackgroundColor = Color.FromArgb(225, 14, 69, 131);
                ForegroundColor = Color.FromArgb(255, 173, 192, 211);
            }
        }

        [Export(typeof(EditorFormatDefinition))]
        [Name(PredefinedMarkerFormatNames.DefinitionIdentifierLight)]
        [UserVisible(true)]
        internal sealed class DefinitionIdentifierHighlightFormatLight : MarkerFormatDefinition
        {
            public DefinitionIdentifierHighlightFormatLight()
            {
                BackgroundColor = Color.FromArgb(255, 219, 224, 204);
            }
        }

        [Export(typeof(EditorFormatDefinition))]
        [Name(PredefinedMarkerFormatNames.DefinitionIdentifierDark)]
        [UserVisible(true)]
        internal sealed class DefinitionIdentifierHighlightFormatDark : MarkerFormatDefinition
        {
            public DefinitionIdentifierHighlightFormatDark()
            {
                BackgroundColor = Color.FromArgb(255, 72, 131, 14);
                ForegroundColor = Color.FromArgb(255, 192, 211, 173);
            }
        }

        [Export(typeof(EditorFormatDefinition))]
        [Name(PredefinedMarkerFormatNames.BraceMatchingLight)]
        [UserVisible(true)]
        internal sealed class BraceMatchingHighlightFormatLight : MarkerFormatDefinition
        {
            public BraceMatchingHighlightFormatLight()
            {
                BackgroundColor = Color.FromArgb(255, 219, 224, 204);
            }
        }

        [Export(typeof(EditorFormatDefinition))]
        [Name(PredefinedMarkerFormatNames.BraceMatchingDark)]
        [UserVisible(true)]
        internal sealed class BraceMatchingHighlightFormatDark : MarkerFormatDefinition
        {
            public BraceMatchingHighlightFormatDark()
            {
                BackgroundColor = Color.FromArgb(225, 14, 69, 131);
                ForegroundColor = Color.FromArgb(255, 173, 192, 211);
            }
        }
        #endregion

#pragma warning restore 169
    }
}
