using System;
using System.Collections.Generic;
using System.Linq;
using static VSRAD.Syntax.Options.GeneralOptionPage;

namespace VSRAD.Syntax.FunctionList
{
    public static class Helper
    {
        public static IEnumerable<FunctionListItem> SortAndFilter(List<FunctionListItem> items, SortState sortState, string filterText)
        {
            Sort(sortState, items);
            return Filter(items, filterText);
        }

        public static IEnumerable<FunctionListItem> Filter(List<FunctionListItem> items, string filterText) =>
            string.IsNullOrEmpty(filterText) 
                ? items 
                : items.Where(t => t.Text.Contains(filterText));

        public static void Sort(SortState sortState, List<FunctionListItem> items)
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
