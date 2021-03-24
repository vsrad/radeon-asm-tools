using System;
using System.Collections.Specialized;

namespace VSRAD.Syntax.Core.Helper
{
    public class FixedSizeDictionary<TKey, TValue>
    {
        private readonly OrderedDictionary _orderedDictionary;

        public FixedSizeDictionary(int capacity)
        {
            _orderedDictionary = new OrderedDictionary(capacity);
        }

        public bool TryGetValue(TKey key, out TValue value)
        {
            value = (TValue)_orderedDictionary[key];
            return value != null;
        }

        public void AddValue(TKey key, Func<TValue> valueFactory)
        {
            _orderedDictionary[key] = valueFactory.Invoke();
        }

        public void Clear()
        {
            _orderedDictionary.Clear();
        }
    }
}