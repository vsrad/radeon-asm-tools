namespace VSRAD.Syntax.Parser.Tokens
{
    public enum RadAsmTokenType
    {
        Comment,
        Identifier,
        String,
        Number,
        Operation,
        Structural,
        Comma,
        Semi,
        Colon,
        Lparen,
        Rparen,
        LsquareBracket,
        RsquareBracket,
        LcurveBracket,
        RcurveBracket,
        Whitespace,
        Keyword,
        FunctionName,
        FunctionReference,
        FunctionParameter,
        FunctionParameterReference,
        Label,
        LabelReference,
        Instruction,
        GlobalVariable,
        LocalVariable,
        Include,
        Preprocessor,
        Unknown,
    }

    public static class RadAsmTokenTypeExtension
    {
        public static string GetName(this RadAsmTokenType type)
        {
            switch (type)
            {
                case RadAsmTokenType.FunctionParameter:
                    return "argument";
                case RadAsmTokenType.Label:
                    return "label";
                case RadAsmTokenType.GlobalVariable:
                    return "global variable";
                case RadAsmTokenType.LocalVariable:
                    return "local variable";
                case RadAsmTokenType.FunctionName:
                    return "function";
                case RadAsmTokenType.Instruction:
                    return "instruction";
                default:
                    return "unknown";
            }
        }
    }
}
