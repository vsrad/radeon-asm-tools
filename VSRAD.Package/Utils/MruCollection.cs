using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;

namespace VSRAD.Package.Utils
{
    public sealed class MruCollection<T> : IReadOnlyCollection<T>, ICollection<T>
    {
        public int Count => _list.Count;
        public int MaxCount { get; }
        public bool IsReadOnly => false;

        public T this[int index] { get => _list[index]; }

        private readonly List<T> _list;

        public MruCollection(int maxCount)
        {
            MaxCount = maxCount;
            _list = new List<T>(maxCount);
        }

        public void Add(T item)
        {
            if (_list.Contains(item))
            {
                /* Put the item at the start of the list */
                _list.Remove(item);
                _list.Insert(0, item);
            }
            else
            {
                if (_list.Count == MaxCount)
                    _list.RemoveAt(MaxCount - 1);

                _list.Insert(0, item);
            }
        }

        public IEnumerator<T> GetEnumerator() => _list.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable)_list).GetEnumerator();

        public void AddRange(IEnumerable<T> collection) => _list.AddRange(collection);

        public void Clear() => _list.Clear();

        public bool Contains(T item) => _list.Contains(item);

        public void CopyTo(T[] array, int arrayIndex) => _list.CopyTo(array, arrayIndex);

        public bool Remove(T item) => _list.Remove(item);

        public sealed class Converter : JsonConverter
        {
            public override bool CanConvert(Type objectType) => objectType == typeof(MruCollection<T>);

            public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
            {
                var items = serializer.Deserialize<List<T>>(reader);
                items.Reverse();
                foreach (var item in items)
                    ((MruCollection<T>)existingValue).Add(item);
                return existingValue;
            }

            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer) =>
                serializer.Serialize(writer, ((MruCollection<T>)value)._list);
        }
    }
}
