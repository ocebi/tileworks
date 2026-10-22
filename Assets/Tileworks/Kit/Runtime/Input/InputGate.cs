using System;
using System.Collections.Generic;
using Tileworks.Kit.Core;

namespace Tileworks.Kit.Input
{
    public interface IInputGate
    {
        bool IsBlocked { get; }
        IDisposable Block(string reason);
        IReadOnlyDictionary<string, int> Locks { get; }
    }

    // Reference-counted named locks: input is open only when no lock is held.
    public sealed class InputGate : IInputGate
    {
        private readonly Dictionary<string, int> _locks = new Dictionary<string, int>();
        private int _total;

        public bool IsBlocked => _total > 0;
        public IReadOnlyDictionary<string, int> Locks => _locks;

        public IDisposable Block(string reason)
        {
            Guard.NotEmpty(reason, nameof(reason));
            _locks.TryGetValue(reason, out var count);
            _locks[reason] = count + 1;
            _total++;
            return new Disposable(() => Release(reason));
        }

        private void Release(string reason)
        {
            if (!_locks.TryGetValue(reason, out var count)) return;
            if (count <= 1) _locks.Remove(reason);
            else _locks[reason] = count - 1;
            _total--;
        }
    }
}
