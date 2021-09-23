using System.Collections.ObjectModel;
using System.Linq;
using VSRAD.Package.Options;

namespace VSRAD.Package.Utils
{
    public static class ActionHelper
    {
        public static string GetNextActionName(ObservableCollection<ActionProfileOptions> actions)
        {
            var currentActionName = "New Action";
            var counter = 0;
            foreach (var action in actions.OrderBy(a => a.Name))
            {
                if (action.Name == currentActionName)
                    currentActionName = $"New Action {++counter}";
            }
            return currentActionName;
        }
    }
}
