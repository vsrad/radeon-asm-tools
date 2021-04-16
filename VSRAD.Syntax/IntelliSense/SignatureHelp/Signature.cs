using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using System.Collections.ObjectModel;
using VSRAD.Syntax.Core.Tokens;

namespace VSRAD.Syntax.IntelliSense.SignatureHelp
{
    public class TextTag
    {
        public RadAsmTokenType Type { get; }
        public Span Span { get; }

        public TextTag(RadAsmTokenType type, Span span)
        {
            Type = type;
            Span = span;
        }
    }

    public interface ISyntaxSignature : ISignature
    {
        ReadOnlyCollection<TextTag> DisplayParts { get; }
        void SetCurrentParameter(int idx);
    }
}
