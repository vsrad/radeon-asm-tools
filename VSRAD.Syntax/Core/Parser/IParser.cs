using Microsoft.VisualStudio.Text;
using System.Threading;
using System.Threading.Tasks;
using VSRAD.Syntax.Core.Helper;
using VSRAD.Syntax.Core.Tokens;
using VSRAD.Syntax.Helpers;
using VSRAD.Syntax.Options.Instructions;

namespace VSRAD.Syntax.Core.Parser
{
    public interface IParser
    {
        Task<IParserResult> RunAsync(IDocument document, ITextSnapshot version, ITokenizerCollection<TrackingToken> tokens, CancellationToken cancellation);
    }

    public interface IAsmParser : IParser
    {
        void UpdateInstructions(IInstructionListManager sender, AsmType asmType);
    }
}
