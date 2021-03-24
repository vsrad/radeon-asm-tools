using VSRAD.Syntax.Core.Tokens;
using VSRAD.Syntax.Helpers;
using VSRAD.Syntax.IntelliSense.Navigation;

namespace VSRAD.Syntax.FunctionList
{
    public class FunctionListItem : DefaultNotifyPropertyChanged
    {
        public FunctionListItemType Type { get; }
        public string Text { get; }
        public int LineNumber { get; }

        private bool _isCurrentWorkingItem;
        private readonly INavigationToken _navigationToken;

        public bool IsCurrentWorkingItem
        {
            get => _isCurrentWorkingItem;
            set => OnPropertyChanged(ref _isCurrentWorkingItem, value);
        }

        public FunctionListItem(INavigationToken navigationToken)
        {
            Type = GetType(navigationToken.Type);
            Text = navigationToken.AnalysisToken.Text;
            // line number starts from 1
            LineNumber = navigationToken.Line.LineNumber + 1;
            _isCurrentWorkingItem = false;
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
