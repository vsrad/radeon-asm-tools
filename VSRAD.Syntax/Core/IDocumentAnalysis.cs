using System;
using Microsoft.VisualStudio.Text;
using System.Threading;
using System.Threading.Tasks;

namespace VSRAD.Syntax.Core
{
    public delegate void AnalysisUpdatedEventHandler(AnalysisResult analysisResult, RescanReason reason, Span updatedTokenSpan, CancellationToken cancellationToken);

    public interface IDocumentAnalysis : IDisposable
    {
        AnalysisResult CurrentResult { get; }
        Task<AnalysisResult> GetAnalysisResultAsync(ITextSnapshot textSnapshot);

        event AnalysisUpdatedEventHandler AnalysisUpdated;
    }
}
