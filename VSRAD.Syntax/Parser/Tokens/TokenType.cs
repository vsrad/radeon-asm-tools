using Microsoft.VisualStudio.Language.Intellisense;

namespace VSRAD.Syntax.Parser.Tokens
{
    public enum TokenType
    {
        Function = 1,
        GlobalVariable = 2,
        LocalVariable = 3,
        Argument = 4,
        Comment = 5,
        Label = 6,
    }

    public static class TokenTypeMapping
    {
        public static string GetName(this TokenType tokenType)
        {
            switch (tokenType)
            {
                case TokenType.Argument:
                    return "argument";
                case TokenType.Function:
                    return "function";
                case TokenType.Label:
                    return "label";
                case TokenType.GlobalVariable:
                    return "global variable";
                case TokenType.LocalVariable:
                    return "function";
                default:
                    return "unknown";
            }
        }

        public static StandardGlyphGroup GetGlyphGroup(this TokenType tokenType)
        {
            switch (tokenType)
            {
                case TokenType.Argument:
                    return StandardGlyphGroup.GlyphGroupValueType;
                case TokenType.Function:
                    return StandardGlyphGroup.GlyphExtensionMethod;
                case TokenType.Label:
                    return StandardGlyphGroup.GlyphGroupNamespace;
                case TokenType.GlobalVariable:
                    return StandardGlyphGroup.GlyphGroupVariable;
                case TokenType.LocalVariable:
                    return StandardGlyphGroup.GlyphGroupValueType;
                default:
                    return StandardGlyphGroup.GlyphGroupUnknown;
            }
        }
    }
}
