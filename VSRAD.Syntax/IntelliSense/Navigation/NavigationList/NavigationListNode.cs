using System.Collections.ObjectModel;

namespace VSRAD.Syntax.IntelliSense.Navigation.NavigationList
{
    public class NavigationListNode
    {
        public string Text { get; }
        public ObservableCollection<NavigationListNode> Items { get; }

        public NavigationListNode(string name)
        {
            Text = name;
            Items = new ObservableCollection<NavigationListNode>();
        }
    }

    public class NavigationListItemNode : NavigationListNode
    {
        public INavigationToken NavigationToken { get; }
        public NavigationListItemNode(INavigationToken token) 
            : base($"{token.Line.LineNumber + 1}: {token.Line.LineText}")
        {
            NavigationToken = token;
        }
    }
}
