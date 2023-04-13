using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VSRAD.Package.DebugVisualizer;

namespace VSRAD.Package.Utils
{
    public enum SelectorType
    {
        First, Last, Custom
    }

    public class VisibleColumnsRange
    {
        public SelectorType Type;
        public int X;
        public int Y;
        public string Custom;

        public VisibleColumnsRange(SelectorType type, int x, int y)
        {
            Type = type; X = x; Y = y;
        }

        public VisibleColumnsRange(string custom)
        {
            Type = SelectorType.Custom;
            Custom = custom;
        }

        public string GetStringRepresentation(uint groupSize)
        {
            if (Type == SelectorType.Custom) return Custom;

            int cur = Type == SelectorType.First ? 0 : Y - X;
            var sb = new StringBuilder();
            while (cur < groupSize)
            {
                sb.Append($"{cur}-{cur+X-1}:");
                cur += Y;
            }
            return sb.ToString();
        }

        public IEnumerable<int> GetRangeRepresentation(uint groupSize)
        {
            if (Type == SelectorType.Custom) return ColumnSelector.ToIndexes(Custom, (int)groupSize);

            int cur = Type == SelectorType.First ? 0 : Y - X;
            var res = new List<int>();
            while (cur < groupSize)
            {
                res.AddRange(Enumerable.Range(cur, X));
                cur += Y;
            }
            return res;
        }
    }
}
