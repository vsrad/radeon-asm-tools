using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VSRAD.Package.Utils
{
    public sealed class RecentlyUsedCollection : ObservableCollection<string>
    {
        private const int MAX_COUNT = 10;

        public void AddElement(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return;
            if (IndexOf(value) is var oldIndex && oldIndex != -1)
            {
                Move(oldIndex, 0);
                return;
            }
            Insert(0, value);
            if (Count > MAX_COUNT) RemoveAt(MAX_COUNT);
        }

    }
}
