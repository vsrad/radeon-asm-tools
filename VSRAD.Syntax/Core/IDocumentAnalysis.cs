using Microsoft.VisualStudio.Text;
using System.Threading.Tasks;
using VSRAD.Syntax.Core.Tokens;

namespace VSRAD.Syntax.Core
{
    public delegate void AnalysisUpdatedEventHandler(IAnalysisResult analysisResult);

    public interface IDocumentAnalysis
    {
        Task<IAnalysisResult> GetAnalysisResultAsync(ITextSnapshot textSnapshot);
        Task<AnalysisToken> GetTokenAsync(SnapshotPoint point);

        event AnalysisUpdatedEventHandler AnalysisUpdated;
    }
}
