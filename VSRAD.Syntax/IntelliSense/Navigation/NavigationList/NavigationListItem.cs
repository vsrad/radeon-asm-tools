using System.Collections.ObjectModel;

namespace VSRAD.Syntax.IntelliSense.Navigation.NavigationList
{
    public class NavigationListItem
    {
        public string Name { get; private set; }
        public ObservableCollection<NavigationListItem> Items { get; private set; }

        public NavigationListItem(string name)
        {
            Name = name;
            Items = new ObservableCollection<NavigationListItem>();
        }
    }

    public class NavigationListDefinitionItem : NavigationListItem
    {
        public DefinitionToken DefinitionToken { get; }
        public NavigationListDefinitionItem(DefinitionToken definitionToken) 
            : base($"{definitionToken.LineNumber + 1}: {definitionToken.Line.GetText()}")
        {
            DefinitionToken = definitionToken;
        }
    }
}
