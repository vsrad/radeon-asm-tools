//using Microsoft.VisualStudio.Language.Intellisense;
//using Microsoft.VisualStudio.Text;
//using Microsoft.VisualStudio.Utilities;
//using System;
//using System.ComponentModel.Composition;

//namespace VSRAD.Syntax.IntelliSense.SignatureHelper
//{
//    [Export(typeof(ISignatureHelpSourceProvider))]
//    [ContentType(Constants.RadeonAsmSyntaxContentType)]
//    [Name(nameof(SignatureHelpSourceProvider))]
//    internal sealed class SignatureHelpSourceProvider : ISignatureHelpSourceProvider
//    {
//        public SignatureHelpSourceProvider()
//        {

//        }

//        public ISignatureHelpSource TryCreateSignatureHelpSource(ITextBuffer textBuffer)
//        {
//            if (textBuffer == null)
//                throw new ArgumentNullException(nameof(textBuffer));

//            return new SignatureHelpSource(textBuffer);
//        }
//    }
//}
