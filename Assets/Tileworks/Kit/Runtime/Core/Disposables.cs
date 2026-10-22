using System;
using System.Collections.Generic;

namespace Tileworks.Kit.Core
{
    public sealed class Disposable : IDisposable
    {
        public static readonly IDisposable Empty = new Disposable(null);

        private Action _onDispose;

        public Disposable(Action onDispose) => _onDispose = onDispose;

        public void Dispose()
        {
            var action = _onDispose;
            _onDispose = null;
            action?.Invoke();
        }
    }

    public sealed class CompositeDisposable : IDisposable
    {
        private readonly List<IDisposable> _items = new List<IDisposable>(8);
        private bool _disposed;

        public int Count => _items.Count;

        public T Add<T>(T item) where T : IDisposable
        {
            if (_disposed)
            {
                item?.Dispose();
                return item;
            }

            if (item != null) _items.Add(item);
            return item;
        }

        public void Clear()
        {
            for (var i = _items.Count - 1; i >= 0; i--) _items[i].Dispose();
            _items.Clear();
        }

        public void Dispose()
        {
            if (_disposed) return;
            Clear();
            _disposed = true;
        }
    }
}
