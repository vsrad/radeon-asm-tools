using Microsoft.VisualStudio.Text;
using System.Threading;
using System.Threading.Tasks;

namespace VSRAD.Syntax.Core
{
    public delegate void AnalysisUpdatedEventHandler(IAnalysisResult analysisResult, RescanReason reason, CancellationToken cancellationToken);

    public interface IDocumentAnalysis
    {
        IAnalysisResult CurrentResult { get; }
        Task<IAnalysisResult> GetAnalysisResultAsync(ITextSnapshot textSnapshot);
        void Rescan(RescanReason rescanReason, CancellationToken cancellationToken);
        void OnDestroy();

        event AnalysisUpdatedEventHandler AnalysisUpdated;
    }
}
