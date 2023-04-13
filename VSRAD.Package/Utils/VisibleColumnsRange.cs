using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VSRAD.Package.Utils
{
    public enum SelectorType
    {
        First, Last, Custom
    }

    class VisibleColumnsRange
    {
        public SelectorType Type;
        public int X;
        public int Y;
        public string Custom;

        public VisibleColumnsRange(SelectorType type, int x, int y)
        {
            Type = type; X = x; Y = y;
        }

        public string GetRepresentation(uint groupSize, List<int> range = null)
        {
            if (Type == SelectorType.Custom) return Custom;

            int cur = Type == SelectorType.First ? 0 : Y - X;
            var sb = new StringBuilder();
            while (cur < groupSize)
            {
                sb.Append($"{cur}-{cur+X-1}:");
                if (range != null) range.AddRange(Enumerable.Range(cur, X));
                cur += Y;
            }
            return sb.ToString();
        }
    }
}
