using System.Collections.Generic;
using System.Linq;

namespace VSRAD.Syntax.FunctionList
{
    public static class Helper
    {
        public static IList<FunctionListItem> Filter(IEnumerable<FunctionListItem> items, TypeFilterState filterType, string filterText) =>
            items.FilterText(filterText).FilterType(filterType).ToList();

        private static IEnumerable<FunctionListItem> FilterText(this IEnumerable<FunctionListItem> items, string filterText) =>
            items.Where(t => t.Text.Contains(filterText));

        private static IEnumerable<FunctionListItem> FilterType(this IEnumerable<FunctionListItem> items, TypeFilterState filterType)
        {
            switch (filterType)
            {
                case TypeFilterState.F: return items.Where(t => t.Type == FunctionListItemType.Function);
                case TypeFilterState.L: return items.Where(t => t.Type == FunctionListItemType.Label);
                default: return items;
            }
        }
    }
}
