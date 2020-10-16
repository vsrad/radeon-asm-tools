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

        public void AddValue(TKey key, Func<TValue> valueFactory)
        {
            orderedDictionary[key] = valueFactory.Invoke();
        }
    }
}