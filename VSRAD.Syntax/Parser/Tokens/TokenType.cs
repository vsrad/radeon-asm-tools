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
        Whitespace,
        Keyword,
        FunctionName,
        FunctionParameter,
        FunctionParameterReference,
        Label,
        Instruction,
        GlobalVariable,
        LocalVariable,
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
                default:
                    return "unknown";
            }
        }
    }
}
