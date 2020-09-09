using System;
using System.Collections.Specialized;

namespace VSRAD.Syntax.Core.Helper
{
    public class FixedSizeDictionary<K, V>
    {
        private OrderedDictionary orderedDictionary;

        public FixedSizeDictionary(int capacity)
        {
            orderedDictionary = new OrderedDictionary(capacity);
        }

        public bool TryGetValue(K key, out V value)
        {
            value = (V)orderedDictionary[key];
            return value != null;
        }

        public bool TryAddValue(K key, Func<V> valueFactory)
        {
            if (!orderedDictionary.Contains(key))
            {
                orderedDictionary[key] = valueFactory.Invoke();
                return true;
            }

            return false;
        }
    }
}