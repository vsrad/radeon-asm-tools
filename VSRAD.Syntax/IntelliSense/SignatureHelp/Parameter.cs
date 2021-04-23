using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;

namespace VSRAD.Syntax.IntelliSense.SignatureHelp
{
    internal class Parameter : IParameter
    {
        public Parameter(ISignature signature, string name, string doc, Span locus, Span ppLocus)
        {
            Signature = signature;
            Name = name;
            Documentation = doc;
            Locus = locus;
            PrettyPrintedLocus = ppLocus;
        }

        public ISignature Signature { get; }

        public string Name { get; }

        public string Documentation { get; }

        public Span Locus { get; }

        public Span PrettyPrintedLocus { get; }
    }
}
