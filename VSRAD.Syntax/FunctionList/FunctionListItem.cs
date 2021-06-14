using VSRAD.Syntax.Core.Tokens;
using VSRAD.Syntax.IntelliSense.Navigation;

namespace VSRAD.Syntax.FunctionList
{
    public class FunctionListItem
    {
        public FunctionListItemType Type { get; }
        public string Text { get; }
        public int LineNumber { get; }

        private readonly INavigationToken _navigationToken;

        public FunctionListItem(INavigationToken navigationToken)
        {
            Type = GetType(navigationToken.Type);
            Text = navigationToken.Definition.GetText();
            // line number starts from 1
            LineNumber = navigationToken.GetLine().LineNumber + 1;
            _navigationToken = navigationToken;
        }

        public void Navigate() => _navigationToken.Navigate();

        private static FunctionListItemType GetType(RadAsmTokenType tokenType) =>
            tokenType == RadAsmTokenType.FunctionName ? FunctionListItemType.Function
                : tokenType == RadAsmTokenType.Label ? FunctionListItemType.Label
                    : throw new System.ArgumentException("Incorrect token type");
    }

    public enum FunctionListItemType
    {
        Function,
        Label,
    }
}
