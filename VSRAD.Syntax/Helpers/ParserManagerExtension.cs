using Microsoft.VisualStudio.Text;
using System.Linq;
using VSRAD.Syntax.Parser;

namespace VSRAD.Syntax.Helpers
{
    internal static class ParserManagerExtension
    {
        public static void InitializeAsm1(this IParserManager parserManager, ITextBuffer textBuffer) =>
            parserManager.Initialize(textBuffer,
                Constants.asm1Start.Concat(Constants.preprocessorStart).ToArray(),
                Constants.asm1End.Concat(Constants.preprocessorEnd).ToArray(),
                Constants.asm1Middle.Concat(Constants.preprocessorMiddle).ToArray(),
                Constants.asm1FunctionKeyword,
                Constants.asm1FunctionDefinitionRegular,
                Constants.asm1MultilineCommentStart,
                Constants.asm1MultilineCommentEnd,
                Constants.asm1CommentStart,
                declorationStartPattern: null,
                declorationEndPattern: null,
                enableManyLineDecloration: false,
                Constants.asm1VariableDefinition,
                Constants.asm1LabelDefinitionRegular);

        public static void InitializeAsm2(this IParserManager parserManager, ITextBuffer textBuffer) =>
            parserManager.Initialize(textBuffer,
                Constants.asm2Start.Concat(Constants.preprocessorStart).ToArray(),
                Constants.asm2End.Concat(Constants.preprocessorEnd).ToArray(),
                Constants.asm2Middle.Concat(Constants.preprocessorMiddle).ToArray(),
                Constants.asm2FunctionKeyword,
                Constants.asm2FunctionDefinitionRegular,
                Constants.asm2MultilineCommentStart,
                Constants.asm2MultilineCommentEnd,
                Constants.asm2CommentStart,
                Constants.asm2FunctionDeclorationStartPattern,
                Constants.asm2FunctionDefinitionEndPattern,
                enableManyLineDecloration: true,
                Constants.asm2VariableDefinition,
                Constants.asm2LabelDefinitionRegular);
    }
}
