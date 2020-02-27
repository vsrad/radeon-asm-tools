using Microsoft.VisualStudio.Text.Editor;
using System.Linq;
using VSRAD.Syntax.Parser.Blocks;

namespace VSRAD.Syntax.Helpers
{
    public static class TextViewParserExtension
    {
        public static FunctionBlock GetFunctionBlockByName(this IWpfTextView view, string name)
        {
            var parserManager = view.GetParserManager();
            var parser = parserManager.ActualParser;

            if (parser == null)
                return null;

            return parser.GetFunctionBlocks()
                .FirstOrDefault(fb => fb.FunctionToken.TokenName == name);
        }
    }
}
