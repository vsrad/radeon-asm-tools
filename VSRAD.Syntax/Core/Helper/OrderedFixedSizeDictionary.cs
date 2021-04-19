using System.Collections.Generic;
using System.Linq;

namespace VSRAD.Syntax.Core.Helper
{
    public class OrderedFixedSizeDictionary<TKey, TValue> where TKey : class
    {
        public int Count => _values.Count;

        private readonly LinkedList<KeyValuePair<TKey, TValue>> _values;
        private readonly int _maxSize;

        public OrderedFixedSizeDictionary(int maxSize)
        {
            _values = new LinkedList<KeyValuePair<TKey, TValue>>();
            _maxSize = maxSize;
        }

        public bool ContainsKey(TKey key) =>
            _values.Any(p => p.Key == key);

        public void Add(TKey key, TValue value)
        {
            _values.AddFirst(new KeyValuePair<TKey, TValue>(key, value));
            if (_values.Count > _maxSize)
                _values.RemoveLast();
        }

        public bool Remove(TKey key)
        {
            var pair = _values.Where(p => p.Key == key);
            if (!pair.Any()) return false;

            _values.Remove(pair.First());
            return true;
        }

        public bool TryGetValue(TKey key, out TValue value)
        {
            var pair = _values.Where(p => p.Key == key);
            if (pair.Any())
            {
                value = pair.First().Value;
                return true;
            }

            value = default;
            return false;
        }
    }
}