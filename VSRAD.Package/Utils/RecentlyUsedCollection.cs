using System.Collections.ObjectModel;

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

        public void AddElement(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return;

            var newElement = new LastUsed(value);
            if (IndexOf(newElement) is var oldIndex && oldIndex != -1)
            {
                Move(oldIndex, 0);
                return;
            }
            Insert(0, newElement);
            if (Count > MAX_COUNT) RemoveAt(MAX_COUNT);
        }

    }
}
