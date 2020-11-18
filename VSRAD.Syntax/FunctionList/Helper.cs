using System;
using System.Collections.Generic;
using System.Linq;
using static VSRAD.Syntax.Options.GeneralOptionPage;

namespace VSRAD.Syntax.FunctionList
{
    public static class Helper
    {
        static int CompareByLine(FunctionListItem a, FunctionListItem b) => a.LineNumber.CompareTo(b.LineNumber);
        static int CompareByLineDesc(FunctionListItem a, FunctionListItem b) => -CompareByLine(a, b);
        static int CompareByName(FunctionListItem a, FunctionListItem b)
        {
            var comapre = string.Compare(a.Text, b.Text, StringComparison.Ordinal);
            return comapre == 0 ? CompareByLine(a, b) : comapre;
        }
        static int CompareByNameDesc(FunctionListItem a, FunctionListItem b) => -CompareByName(a, b);

        public static IList<FunctionListItem> FilterAndSort(IEnumerable<FunctionListItem> items, SortState sortState, TypeFilterState filterType, string filterText)
        {
            items = FilterText(items, filterText);
            items = FilterType(items, filterType);

            var filteredValues = items.AsParallel().ToList();
            Sort(filteredValues, sortState);
            return filteredValues;
        }

        public static IEnumerable<FunctionListItem> FilterText(IEnumerable<FunctionListItem> items, string filterText) =>
            items.Where(t => t.Text.Contains(filterText));

        public static IEnumerable<FunctionListItem> FilterType(IEnumerable<FunctionListItem> items, TypeFilterState filterType)
        {
            switch (filterType)
            {
                case TypeFilterState.F: return items.Where(t => t.Type == FunctionListItemType.Function);
                case TypeFilterState.L: return items.Where(t => t.Type == FunctionListItemType.Label);
                default: return items;
            }
        }

        public static void Sort(List<FunctionListItem> items, SortState sortState)
        {
            Func<FunctionListItem, FunctionListItem, int> comparison;
            switch (sortState)
            {
                case SortState.ByLine: comparison = CompareByLine; break;
                case SortState.ByName: comparison = CompareByName; break;
                case SortState.ByLineDescending: comparison = CompareByLineDesc; break;
                case SortState.ByNameDescending: comparison = CompareByNameDesc; break;
                default: return;
            }
            items.Sort(new Comparison<FunctionListItem>(comparison));
        }
    }
}
