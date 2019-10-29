using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;

namespace VSRAD.Package.Utils
{
    public interface IObservableReadOnlyDictionary<TKey, TValue> : IReadOnlyDictionary<TKey, TValue>, INotifyCollectionChanged, INotifyPropertyChanged { }

    // Adapted from http://blogs.microsoft.co.il/shimmy/2010/12/26/observabledictionarylttkey-tvaluegt-c/
    public class ObservableDictionary<TKey, TValue> : IDictionary<TKey, TValue>, IObservableReadOnlyDictionary<TKey, TValue>
    {
        private readonly Dictionary<TKey, TValue> _dictionary = new Dictionary<TKey, TValue>();

        #region Collection Interfaces
        public void Add(TKey key, TValue value) => Insert(key, value, throwIfExists: true);
        public void Add(KeyValuePair<TKey, TValue> item) => Insert(item.Key, item.Value, throwIfExists: true);

        public bool ContainsKey(TKey key) => _dictionary.ContainsKey(key);
        public bool Contains(KeyValuePair<TKey, TValue> item) => _dictionary.Contains(item);

        public ICollection<TKey> Keys => _dictionary.Keys;
        IEnumerable<TKey> IReadOnlyDictionary<TKey, TValue>.Keys => _dictionary.Keys;

        public ICollection<TValue> Values => _dictionary.Values;
        IEnumerable<TValue> IReadOnlyDictionary<TKey, TValue>.Values => _dictionary.Values;

        public bool Remove(TKey key)
        {
            if (_dictionary.Remove(key))
            {
                OnCollectionChanged();
                return true;
            }
            return false;
        }

        public bool TryGetValue(TKey key, out TValue value) => _dictionary.TryGetValue(key, out value);

        public TValue this[TKey key]
        {
            get => _dictionary[key];
            set => Insert(key, value, throwIfExists: false);
        }

        public void Clear()
        {
            if (_dictionary.Count == 0) return;
            _dictionary.Clear();
            OnCollectionChanged();
        }

        public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex) => ((ICollection<KeyValuePair<TKey, TValue>>)_dictionary).CopyTo(array, arrayIndex);

        public int Count => _dictionary.Count;

        public bool IsReadOnly => ((ICollection<KeyValuePair<TKey, TValue>>)_dictionary).IsReadOnly;

        public bool Remove(KeyValuePair<TKey, TValue> item) => Remove(item.Key);

        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() => _dictionary.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable)_dictionary).GetEnumerator();
        #endregion

        public event NotifyCollectionChangedEventHandler CollectionChanged;

        public event PropertyChangedEventHandler PropertyChanged;

        private void Insert(TKey key, TValue value, bool throwIfExists)
        {
            if (_dictionary.TryGetValue(key, out var item))
            {
                if (throwIfExists) throw new ArgumentException("An item with the same key has already been added.");
                if (Equals(item, value)) return;
            }
            _dictionary[key] = value;
            OnCollectionChanged();
        }

        private void OnCollectionChanged()
        {
            OnCollectionPropertiesChanged();
            CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }

        private void OnCollectionPropertiesChanged()
        {
            OnPropertyChanged(nameof(Count));
            OnPropertyChanged(nameof(Keys));
            OnPropertyChanged(nameof(Values));
        }

        private void OnPropertyChanged(string propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
