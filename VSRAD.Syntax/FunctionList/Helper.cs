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
