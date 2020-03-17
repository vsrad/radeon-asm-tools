using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;
using System.ComponentModel.Composition;
using System.Linq;
using VSRAD.Syntax.Helpers;

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
            parserManager.InitializeAsm1(textView.TextBuffer);

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
            parserManager.InitializeAsm2(textView.TextBuffer);

            // TODO fix this
            parserManager.ParserUpdatedEvent += async (sender, args) => await FunctionList.FunctionList.TryUpdateFunctionListAsync(sender);
            textView.Options.OptionChanged += (sender, args) => parserManager.TabSize = textView.Options.GetOptionValue(DefaultOptions.TabSizeOptionId);
        }
    }
}
