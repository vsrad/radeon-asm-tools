using Microsoft.VisualStudio.Text;
using System;
using System.Text;
using Microsoft.VisualStudio.Shell;
using VSRAD.Syntax.Core;
using VSRAD.Syntax.Core.Tokens;
using VSRAD.Syntax.Helpers;

namespace VSRAD.Syntax.IntelliSense.Navigation
{
    public interface ITokenLine
    {
        int LineStart { get; }
        int LineEnd { get; }
        int LineNumber { get; }
        string LineText { get; }
    }

    public interface INavigationToken
    {
        IDocument Document { get; }
        IAnalysisToken AnalysisToken { get; }
        RadAsmTokenType Type { get; }

        int GetStart();
        int GetEnd();
        ITokenLine GetLine();
        void Navigate();
    }

    internal readonly struct TokenLine : ITokenLine
    {
        private readonly Lazy<ITextSnapshotLine> _lineLazy;

        public TokenLine(IAnalysisToken token, ITextSnapshot snapshot)
        {
            _lineLazy = new Lazy<ITextSnapshotLine>(() =>
            {
                var start = token.TrackingToken.GetStart(snapshot);
                return snapshot.GetLineFromPosition(start);
            });
        }

        public int LineStart => _lineLazy.Value.Start;
        public int LineEnd => _lineLazy.Value.End;
        public int LineNumber => _lineLazy.Value.LineNumber;
        public string LineText => _lineLazy.Value.GetText();
    }

    public class NavigationToken : INavigationToken
    {
        public IDocument Document { get; }
        public IAnalysisToken AnalysisToken { get; }
        public RadAsmTokenType Type => AnalysisToken.Type;

        public NavigationToken(IAnalysisToken analysisToken, IDocument document)
        {
            Document = document;
            AnalysisToken = analysisToken;
        }

        public ITokenLine GetLine() =>
            new TokenLine(AnalysisToken, Document.CurrentSnapshot);

        public int GetStart() =>
            AnalysisToken.TrackingToken.GetStart(Document.CurrentSnapshot);

        public int GetEnd() =>
            AnalysisToken.TrackingToken.GetEnd(Document.CurrentSnapshot);

        public void Navigate()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            try
            {
                Document.NavigateToPosition(GetEnd());
            }
            catch (Exception)
            {
                Error.ShowErrorMessage("Navigation outdated, please update document content", "Navigation service");
            }
        }

        public override string ToString()
        {
            var line = GetLine();
            var sb = new StringBuilder();
            if (Document.Path != null)
            {
                sb.Append(Document.Path);
                sb.Append(" ");
            }
            sb.Append("(");
            sb.Append(line.LineNumber + 1);
            sb.Append("): ");

            sb.Append(line.LineText);
            return sb.ToString();
        }
    }
}
