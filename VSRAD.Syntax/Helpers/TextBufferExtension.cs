using Microsoft.VisualStudio.Text;
using VSRAD.Syntax.Parser;

namespace VSRAD.Syntax.Helpers
{
    public static class TextBufferExtension
    {
        public static IParserManager GetParserManager(this ITextBuffer textBuffer) =>
            textBuffer.Properties.GetOrCreateSingletonProperty(() => new ParserManger());
    }
}
