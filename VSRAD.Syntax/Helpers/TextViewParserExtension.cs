using Microsoft.VisualStudio.Text.Editor;
using VSRAD.Syntax.Parser.Blocks;
using VSRAD.Syntax.Parser.Tokens;

namespace VSRAD.Syntax.Helpers
{
    public static class TextViewParserExtension
    {
        public static FunctionBlock GetFunctionBlockByName(this IWpfTextView view, IBaseToken token)
        {
            var parserManager = view.GetParserManager();
            var parser = parserManager.ActualParser;

            if (parser == null)
                return null;

            return parser.GetFunction(token);
        }
    }
}
