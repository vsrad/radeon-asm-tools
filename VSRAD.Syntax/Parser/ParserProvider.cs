using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;
using System.ComponentModel.Composition;
using System.Linq;

namespace VSRAD.Syntax.Parser
{
    [Export(typeof(IWpfTextViewCreationListener))]
    [ContentType(Constants.RadeonAsmSyntaxContentType)]
    [TextViewRole(PredefinedTextViewRoles.Editable)]
    [Order(Before = Priority.High)]
    internal class ParserAsm1Provider : IWpfTextViewCreationListener
    {
        public void TextViewCreated(IWpfTextView textView)
        {
            var parserManager = textView
                .TextBuffer
                .Properties
                .GetOrCreateSingletonProperty(() => new ParserManger());

            parserManager.TabSize = textView.Options.GetOptionValue(DefaultOptions.TabSizeOptionId);

            parserManager.Initialize(textView.TextBuffer,
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
                Constants.asm1VariableDefinition);

            // TODO fix this
            parserManager.ParserUpdatedEvent += async (sender, args) => await FunctionList.FunctionList.TryUpdateFunctionListAsync(sender);
            textView.Options.OptionChanged += (sender, args) => parserManager.TabSize = textView.Options.GetOptionValue(DefaultOptions.TabSizeOptionId);
        }
    }

    [Export(typeof(IWpfTextViewCreationListener))]
    [ContentType(Constants.RadeonAsm2SyntaxContentType)]
    [TextViewRole(PredefinedTextViewRoles.Editable)]
    [Order(Before = Priority.High)]
    internal class ParserAsm2Provider : IWpfTextViewCreationListener
    {
        public void TextViewCreated(IWpfTextView textView)
        {
            var parserManager = textView
                .TextBuffer
                .Properties
                .GetOrCreateSingletonProperty(() => new ParserManger());

            parserManager.TabSize = textView.Options.GetOptionValue(DefaultOptions.TabSizeOptionId);

            parserManager.Initialize(textView.TextBuffer,
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
                Constants.asm2VariableDefinition);

            // TODO fix this
            parserManager.ParserUpdatedEvent += async (sender, args) => await FunctionList.FunctionList.TryUpdateFunctionListAsync(sender);
            textView.Options.OptionChanged += (sender, args) => parserManager.TabSize = textView.Options.GetOptionValue(DefaultOptions.TabSizeOptionId);
        }
    }
}
