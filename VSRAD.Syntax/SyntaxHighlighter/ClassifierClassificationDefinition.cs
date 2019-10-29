using System.ComponentModel.Composition;
using System.Windows.Media;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Utilities;



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

        [Export]
        [Name(PredefinedClassificationTypeNames.ExtraKeywords)]
        [BaseDefinition(Constants.RadeonAsmSyntaxContentType)]
        internal static ClassificationTypeDefinition extraKeywordsDefinition = null;

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
        [ClassificationType(ClassificationTypeNames = PredefinedClassificationTypeNames.ExtraKeywords)]
        [Name(PredefinedClassificationFormatNames.ExtraKeywords)]
        [UserVisible(true)]
        internal sealed class ExtraKeywordFormat : ClassificationFormatDefinition
        {
            public ExtraKeywordFormat()
            {
                ForegroundColor = Colors.DarkGray;
            }
        }

        [Export(typeof(EditorFormatDefinition))]
        [Name(PredefinedMarkerFormatNames.IdentifierLight)]
        [UserVisible(true)]
        internal sealed class IdentifierHighlightFormatLight : MarkerFormatDefinition
        {
            public IdentifierHighlightFormatLight()
            {
                BackgroundColor = Color.FromArgb(255, 219, 224, 204);
            }
        }

        [Export(typeof(EditorFormatDefinition))]
        [Name(PredefinedMarkerFormatNames.IdentifierDark)]
        [UserVisible(true)]
        internal sealed class IdentifierHighlightFormatDark : MarkerFormatDefinition
        {
            public IdentifierHighlightFormatDark()
            {
                BackgroundColor = Color.FromArgb(125, 23, 62, 94);
                ForegroundColor = Color.FromArgb(125, 113, 171, 219);
            }
        }
        #endregion

#pragma warning restore 169
    }
}
