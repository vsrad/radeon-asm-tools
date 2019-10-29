using VisualStudioPredefinedClassificationNames = Microsoft.VisualStudio.Language.StandardClassification.PredefinedClassificationTypeNames;

namespace VSRAD.Syntax.SyntaxHighlighter
{
    static class PredefinedMarkerFormatNames
    {
        public const string IdentifierLight = "Radeon asm identifier light theme";
        public const string IdentifierDark = "Radeon asm identifier dark theme";
    }

    static class PredefinedClassificationTypeNames
    {
        public const string Instructions = "radeonAsm.instructions";
        public const string Arguments = "radeonAsm.arguments";
        public const string Functions = "radeonAsm.functions";
        public const string Keywords = VisualStudioPredefinedClassificationNames.Keyword;
        public const string Labels = "radeonAsm.labels";
        public const string Comments = VisualStudioPredefinedClassificationNames.Comment;
        public const string Numbers = VisualStudioPredefinedClassificationNames.Number;
        public const string Strings = VisualStudioPredefinedClassificationNames.String;
        public const string ExtraKeywords = "radeonAsm.extraKeywords";
    }

    static class PredefinedClassificationFormatNames
    {
        public const string Instructions = "Radeon asm instructions";
        public const string Arguments = "Radeon asm function arguments";
        public const string Functions = "Radeon asm function name";
        public const string Labels = "Radeon asm labels";
        public const string ExtraKeywords = "Radeon asm extra keywords (preprocessor and etc)";
    }
}
