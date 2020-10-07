using System;
using System.Collections.Specialized;

namespace VSRAD.Syntax.Core.Helper
{
    public class FixedSizeDictionary<TKey, TValue>
    {
        private readonly OrderedDictionary orderedDictionary;

        public FixedSizeDictionary(int capacity)
        {
            orderedDictionary = new OrderedDictionary(capacity);
        }

        public bool TryGetValue(TKey key, out TValue value)
        {
            value = (TValue)orderedDictionary[key];
            return value != null;
        }

        public bool TryAddValue(TKey key, Func<TValue> valueFactory)
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