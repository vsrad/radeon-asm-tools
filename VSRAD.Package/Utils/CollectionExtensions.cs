using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

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

        public static T ExclusiveOrDefault<T>(this IEnumerable<T> source)
        {
            var elements = source.Take(2).ToList();
            return elements.Count == 1 ? elements[0] : default(T);
        }
    }
}
