using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VSRAD.Package.DebugVisualizer
{
    public static class ColumnSelector
    {
        public static IEnumerable<int> ToIndexes(string selector)
        {
            while (selector.Length > 0)
            {
                string lhs = "", rhs = "";
                foreach (char c in selector)
                {
                    if (char.IsDigit(c)) lhs += c;
                    else break;
                }
                if (lhs.Length == 0) // no digits means we've encountered a separator, skip it
                {
                    selector = selector.Substring(1);
                    continue;
                }
                selector = selector.Substring(lhs.Length);
                if (selector.Length > 0 && selector[0] == '-')
                {
                    selector = selector.Substring(1); // skip '-'
                    foreach (char c in selector)
                    {
                        if (char.IsDigit(c)) rhs += c;
                        else break;
                    }
                    if (int.TryParse(lhs, out var rangeStart) && int.TryParse(rhs, out var rangeEnd))
                    {
                        for (int i = rangeStart; i <= rangeEnd && i < VisualizerTable.DataColumnCount; ++i)
                            yield return i;
                    }
                    selector = selector.Substring(rhs.Length);
                }
                else if (int.TryParse(lhs, out var i) && i < VisualizerTable.DataColumnCount)
                {
                    yield return i;
                }
            }
        }

        public static string FromIndexes(IEnumerable<int> columnIndexes)
        {
            var indexList = columnIndexes.OrderBy(x => x).Distinct().ToList();

            if (indexList.Count == 0) return string.Empty;

            var sb = new StringBuilder();
            sb.Append(indexList[0]);

            bool rangeContinues = false;

            for (int i = 1; i < indexList.Count; ++i)
            {
                if (indexList[i] - indexList[i - 1] == 1)
                {
                    if (!rangeContinues)
                    {
                        sb.Append("-");
                        rangeContinues = true;
                    }
                }
                else
                {
                    if (rangeContinues)
                    {
                        sb.Append(indexList[i - 1]);
                        rangeContinues = false;
                    }
                    sb.Append($":{indexList[i]}");
                }
            }

            if (rangeContinues)
                sb.Append(indexList.Last());

            return sb.ToString();
        }

        public static string PartialSubgroups(uint groupSize, uint subgroupSize, uint displayedCount, bool displayLast = false)
        {
            var sb = new StringBuilder();
            for (uint i = 0; i < groupSize; i += subgroupSize)
            {
                if (displayLast)
                    sb.Append($"{i + subgroupSize - displayedCount}-{i + subgroupSize - 1}:");
                else
                    sb.Append($"{i}-{i + displayedCount - 1}:");
            }
            sb.Length--; // remove last separator

            return sb.ToString();
        }

        public static void RemoveIndexes(IEnumerable<int> columnIndexes, IList<ColumnHighlightRegion> regions)
        {
            foreach (var region in regions)
                region.Selector = FromIndexes(ToIndexes(region.Selector).Except(columnIndexes));

            var emptyRegions = regions.Where(x => string.IsNullOrEmpty(x.Selector)).ToList();
            foreach (var emptyRegion in emptyRegions)
                regions.Remove(emptyRegion);
        }

        public static string GetSelectorMultiplication(string currentSelector, string newSelector)
        {
            var currentIndexes = ToIndexes(currentSelector);
            var newIndexes = ToIndexes(newSelector);

            if (currentIndexes.All(i => newIndexes.Contains(i)))
                return FromIndexes(newIndexes);
            else
                return FromIndexes(newIndexes.Intersect(currentIndexes));
        }
    }
}
