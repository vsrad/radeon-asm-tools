using Microsoft.VisualStudio.Text;
using System.Text;

namespace VSRAD.Syntax.IntelliSense.Navigation.NavigationList
{
    public struct DefinitionToken
    {
        public NavigationToken NavigationToken { get; }
        public string FilePath { get; }
        public int LineNumber { get; }
        public string LineText { get; }

        public DefinitionToken(NavigationToken navigationToken)
        {
            NavigationToken = navigationToken;
            if (NavigationToken.Snapshot.TextBuffer.Properties.TryGetProperty<ITextDocument>(typeof(ITextDocument), out var document))
            {
                FilePath = document.FilePath;
            }
            else
            {
                FilePath = null;
            }

            var line = NavigationToken.Snapshot.GetLineFromPosition(NavigationToken.AnalysisToken.TrackingToken.GetStart(NavigationToken.Snapshot));
            LineNumber = line.LineNumber;
            LineText = line.GetText();
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

            sb.Append(LineText);
            return sb.ToString();
        }
    }
}
