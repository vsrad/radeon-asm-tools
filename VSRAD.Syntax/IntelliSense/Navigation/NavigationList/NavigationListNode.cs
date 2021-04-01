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
            : base($"{token.GetLine().LineNumber + 1}: {token.GetLine().LineText}")
        {
            NavigationToken = token;
        }
    }
}
