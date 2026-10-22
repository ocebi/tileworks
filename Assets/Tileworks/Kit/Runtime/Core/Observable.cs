using System;
using System.Collections.Generic;

namespace Tileworks.Kit.Core
{
    public interface IReadOnlyObservable<out T>
    {
        T Value { get; }

        IDisposable Subscribe(Action<T> onChanged, bool invokeNow = true);
    }

    public sealed class Observable<T> : IReadOnlyObservable<T>
    {
        private readonly IEqualityComparer<T> _comparer;
        private Action<T>[] _handlers = Array.Empty<Action<T>>();
        private T _value;

        public Observable(T initial = default, IEqualityComparer<T> comparer = null)
        {
            _value = initial;
            _comparer = comparer ?? EqualityComparer<T>.Default;
        }

        public T Value
        {
            get => _value;
            set
            {
                if (_comparer.Equals(_value, value)) return;
                _value = value;
                // Snapshot: handlers may unsubscribe while notified.
                var handlers = _handlers;
                for (var i = 0; i < handlers.Length; i++) handlers[i](value);
            }
        }

        public void SetSilently(T value) => _value = value;

        public IDisposable Subscribe(Action<T> onChanged, bool invokeNow = true)
        {
            Guard.NotNull(onChanged, nameof(onChanged));
            var next = new Action<T>[_handlers.Length + 1];
            Array.Copy(_handlers, next, _handlers.Length);
            next[_handlers.Length] = onChanged;
            _handlers = next;
            if (invokeNow) onChanged(_value);
            return new Disposable(() => Unsubscribe(onChanged));
        }

        private void Unsubscribe(Action<T> handler)
        {
            var index = Array.IndexOf(_handlers, handler);
            if (index < 0) return;
            var next = new Action<T>[_handlers.Length - 1];
            Array.Copy(_handlers, 0, next, 0, index);
            Array.Copy(_handlers, index + 1, next, index, _handlers.Length - index - 1);
            _handlers = next;
        }

        public override string ToString() => _value?.ToString() ?? "null";
    }
}
