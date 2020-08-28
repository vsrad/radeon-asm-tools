using Microsoft.VisualStudio.Text;
using System.Text;
using VSRAD.Syntax.Core;

namespace VSRAD.Syntax.IntelliSense.Navigation.NavigationList
{
    public struct DefinitionToken
    {
        public NavigationToken NavigationToken { get; }
        public string FilePath { get; }
        public int LineNumber => Line.LineNumber;
        public ITextSnapshotLine Line { get; }

        public DefinitionToken(NavigationToken navigationToken)
        {
            NavigationToken = navigationToken;
            if (NavigationToken.Snapshot.TextBuffer.Properties.TryGetProperty<DocumentInfo>(typeof(DocumentInfo), out var document))
            {
                FilePath = document.Path;
            }
            else
            {
                FilePath = null;
            }

            Line = NavigationToken.Snapshot.GetLineFromPosition(NavigationToken.AnalysisToken.TrackingToken.GetStart(NavigationToken.Snapshot));
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            if (FilePath != null)
            {
                sb.Append(FilePath);
                sb.Append(" ");
            }
            sb.Append("(");
            sb.Append(LineNumber + 1);
            sb.Append("): ");

            sb.Append(Line.GetText());
            return sb.ToString();
        }
    }
}
