using System;
using System.Collections.Generic;
using System.Linq;
using static VSRAD.Syntax.Options.GeneralOptionPage;

namespace VSRAD.Syntax.FunctionList
{
    public static class Helper
    {
        public static IList<FunctionListItem> FilterAndSort(IEnumerable<FunctionListItem> items, SortState sortState, TypeFilterState filterType, string filterText)
        {
            items = FilterText(items, filterText);
            items = FilterType(items, filterType);

            var filteredValues = items.AsParallel().ToList();
            Sort(filteredValues, sortState);
            return filteredValues;
        }

        private static IEnumerable<FunctionListItem> FilterText(IEnumerable<FunctionListItem> items, string filterText) =>
            items.Where(t => t.Text.Contains(filterText));

        private static IEnumerable<FunctionListItem> FilterType(IEnumerable<FunctionListItem> items, TypeFilterState filterType)
        {
            switch (filterType)
            {
                case TypeFilterState.F: return items.Where(t => t.Type == FunctionListItemType.Function);
                case TypeFilterState.L: return items.Where(t => t.Type == FunctionListItemType.Label);
                default: return items;
            }
        }

        private static void Sort(List<FunctionListItem> items, SortState sortState)
        {
            switch (sortState)
            {
                case SortState.ByLine:
                    items.Sort((a, b) => a.LineNumber.CompareTo(b.LineNumber));
                    break;

                case SortState.ByName:
                    items.Sort((a, b) => string.Compare(a.Text, b.Text, StringComparison.OrdinalIgnoreCase));
                    break;

                case SortState.ByLineDescending:
                    items.Sort((a, b) => b.LineNumber.CompareTo(a.LineNumber));
                    break;

                case SortState.ByNameDescending:
                    items.Sort((a, b) => string.Compare(b.Text, a.Text, StringComparison.OrdinalIgnoreCase));
                    break;
                default:
                    items.Sort((a, b) => a.LineNumber.CompareTo(b.LineNumber));
                    break;
            }
        }
    }
}
