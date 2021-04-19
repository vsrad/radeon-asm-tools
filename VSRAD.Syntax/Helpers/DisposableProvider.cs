using System;
using System.Collections.Generic;

namespace VSRAD.Syntax.Helpers
{
    public interface IDisposableProvider<in TKey, TVal> where TVal : ISyntaxDisposable
    {
        TVal GetValue(TKey key, Func<TVal> factory);
        void DisposeRequest(TKey key);
    }

    public class DisposableProvider<TKey, TVal> : IDisposableProvider<TKey, TVal> where TVal : ISyntaxDisposable
    {
        private readonly Dictionary<TKey, TVal> _keyValuePairs;

        public DisposableProvider()
        {
            _keyValuePairs = new Dictionary<TKey, TVal>();
        }

        public TVal GetValue(TKey key, Func<TVal> factory)
        {
            if (!_keyValuePairs.TryGetValue(key, out var value))
            {
                value = factory.Invoke();
                _keyValuePairs.Add(key, value);
            }

            return value;
        }

        public void DisposeRequest(TKey key)
        {
            if (!_keyValuePairs.TryGetValue(key, out var value))
                return;

            value.OnDispose();
            _keyValuePairs.Remove(key);
        }
    }
}
