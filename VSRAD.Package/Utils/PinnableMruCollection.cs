﻿using System;
using System.Collections.ObjectModel;

namespace VSRAD.Package.Utils
{
    public sealed class PinnableElement<T> : DefaultNotifyPropertyChanged where T : IEquatable<T>
    {
        private T _value;
        public T Value { get => _value; set => SetField(ref _value, value); }

        private bool _pinned;
        public bool Pinned { get => _pinned; set => SetField(ref _pinned, value); }

        public PinnableElement(T value, bool pinned = false)
        {
            Value = value;
            Pinned = pinned;
        }

        // Override Equals for proper behaviour of IndexOf - we want it to rely only on value
        // because we don't know about the pinned state in the execution phase
        public override bool Equals(object obj) => obj is PinnableElement<T> other && other.Value.Equals(Value);
    }

    public sealed class PinnableMruCollection<T> : ObservableCollection<PinnableElement<T>> where T: IEquatable<T>
    {
        private const int MAX_COUNT = 10;
        private int _pinnedCount = 0;

        // Override InsertItem for proper initialization of _pinnedCount
        // when deserializing a JSON
        protected override void InsertItem(int index, PinnableElement<T> item)
        {
            if (item.Pinned) _pinnedCount++;
            base.InsertItem(index, item);
        }

        public void AddElement(T value)
        {
            var newElement = new PinnableElement<T>(value);
            if (IndexOf(newElement) is var oldIndex && oldIndex != -1)
            {
                var newIndex = this[oldIndex].Pinned ? 0 : _pinnedCount;
                if (newIndex == oldIndex) return;
                Move(oldIndex, newIndex);
                return;
            }
            if (_pinnedCount == MAX_COUNT) return; // all elemetns are pinned, do not add new
            if (Count == MAX_COUNT) // at least one unpinned element, but MAX_COUNT reached, replace value of last unpinned element
            {
                UpdateElementsOrder();
                this[Count - 1].Value = newElement.Value;
                if (_pinnedCount != Count - 1) Move(Count - 1, _pinnedCount);
                return;
            }
            Insert(_pinnedCount, newElement);
        }

        public void TogglePinnedState(PinnableElement<T> element)
        {
            if (element.Pinned)
                UnpinElement(element);
            else
                PinElement(element);
        }

        private void PinElement(PinnableElement<T> element)
        {
            if (IndexOf(element) is var oldIndex && oldIndex == -1) return; // No such element in collection
            element.Pinned = true;
            _pinnedCount++;
        }

        private void UnpinElement(PinnableElement<T> element)
        {
            if (IndexOf(element) is var oldIndex && oldIndex == -1) return; // No such element in collection
            element.Pinned = false;
            _pinnedCount--;
        }

        public void UpdateElementsOrder()
        {
            for (int i = 0, pinnedMapped = 0; pinnedMapped != _pinnedCount; i++)
            {
                if (this[i].Pinned)
                {
                    if (i != pinnedMapped) Move(i, pinnedMapped);
                    pinnedMapped++;
                }
            }
        }
    }
}
