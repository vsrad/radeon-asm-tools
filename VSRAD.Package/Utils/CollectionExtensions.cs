using System;
using System.Collections.ObjectModel;

namespace VSRAD.Package.Utils
{
    public static class CollectionExtensions
    {
        public static void RemoveAll<T>(this ObservableCollection<T> collection, Predicate<T> predicate)
        {
            for (int i = collection.Count - 1; i >= 0; i--)
                if (predicate(collection[i]))
                    collection.RemoveAt(i);
        }
    }
}
