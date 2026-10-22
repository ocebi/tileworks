using System;

namespace Tileworks.Kit.Core
{
    public sealed class RingBuffer<T>
    {
        private readonly T[] _items;
        private int _start;

        public RingBuffer(int capacity)
        {
            if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            _items = new T[capacity];
        }

        public int Count { get; private set; }
        public int Capacity => _items.Length;

        public T this[int index]
        {
            get
            {
                if ((uint)index >= (uint)Count) throw new ArgumentOutOfRangeException(nameof(index));
                return _items[(_start + index) % _items.Length];
            }
        }

        public void Push(in T item)
        {
            if (Count < _items.Length)
            {
                _items[(_start + Count) % _items.Length] = item;
                Count++;
                return;
            }

            _items[_start] = item;
            _start = (_start + 1) % _items.Length;
        }

        public void Clear()
        {
            Array.Clear(_items, 0, _items.Length);
            _start = 0;
            Count = 0;
        }
    }
}
