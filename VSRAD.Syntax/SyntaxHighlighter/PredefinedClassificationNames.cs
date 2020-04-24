using VisualStudioPredefinedClassificationNames = Microsoft.VisualStudio.Language.StandardClassification.PredefinedClassificationTypeNames;

namespace VSRAD.Syntax.SyntaxHighlighter
{
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
        public const string Instructions = "RAD instructions";
        public const string Arguments = "RAD function arguments";
        public const string Functions = "RAD function name";
        public const string Labels = "RAD labels";
        public const string ExtraKeywords = "RAD extra keywords (preprocessor and etc)";
    }
    static class PredefinedMarkerFormatNames
    {
        public const string IdentifierLight = "RAD identifier light theme";
        public const string IdentifierDark = "RAD identifier dark theme";
    }
}
