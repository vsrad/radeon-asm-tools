using Newtonsoft.Json;
using System;
using System.Collections.ObjectModel;
using System.Windows.Controls;
using System.Windows.Input;
using System.Linq;

namespace VSRAD.Package.Utils
{
    public sealed class LastUsed : DefaultNotifyPropertyChanged
    {
        private string _value;
        public string Value { get => _value; set => SetField(ref _value, value); }

        private bool _pinned;
        public bool Pinned { get => _pinned; set => SetField(ref _pinned, value); }

        public LastUsed(string value)
        {
            Value = value;
            Pinned = false;
        }
    }

    public sealed class RecentlyUsedCollection : ObservableCollection<LastUsed>
    {
        private const int MAX_COUNT = 10;
        private int _pinnedCount = 0;

        public RecentlyUsedCollection()
        {
            _pinnedCount = this.Count(x => x.Pinned);
        }

        public void AddElement(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return;

            var newElement = new LastUsed(value);
            if (IndexOf(newElement) is var oldIndex && oldIndex != -1)
            {
                MoveItem(oldIndex, _pinnedCount);
                return;
            }
            Insert(_pinnedCount, newElement);
            if (Count > MAX_COUNT) RemoveAt(MAX_COUNT);
        }

        public void PinElement(LastUsed element)
        {
            if (IndexOf(element) is var oldIndex && oldIndex == -1) return; // No such element in collection
            MoveItem(oldIndex, _pinnedCount);
            this[_pinnedCount].Pinned = true;
            _pinnedCount++;
        }

        public void UnpinElement(LastUsed element)
        {
            if (IndexOf(element) is var oldIndex && oldIndex == -1) return; // No such element in collection
            _pinnedCount--;
            MoveItem(oldIndex, _pinnedCount);
            this[_pinnedCount].Pinned = false;
        }
    }
}
