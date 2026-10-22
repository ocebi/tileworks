using System;
using System.Collections.Generic;
using Tileworks.Kit.Core;

namespace Tileworks.Kit.Unity
{
    // The single per-frame entry point; driven by LoopDriver.
    public sealed class UpdateLoop : ILoopRegistry
    {
        private readonly Registry<ITickable> _tickables = new Registry<ITickable>();
        private readonly Registry<ILateTickable> _lateTickables = new Registry<ILateTickable>();
        private readonly Registry<IAppLifecycleListener> _listeners = new Registry<IAppLifecycleListener>();
        private readonly ILog _log;

        public UpdateLoop(ILog log) => _log = Guard.NotNull(log, nameof(log));

        public int TickableCount => _tickables.Count;

        public IDisposable AddTickable(ITickable tickable) => _tickables.Add(tickable);
        public IDisposable AddLateTickable(ILateTickable tickable) => _lateTickables.Add(tickable);
        public IDisposable AddLifecycleListener(IAppLifecycleListener listener) => _listeners.Add(listener);

        public void Tick()
        {
            var items = _tickables.Snapshot();
            for (var i = 0; i < items.Length; i++)
            {
                try
                {
                    items[i].Tick();
                }
                catch (Exception e)
                {
                    _log.Error(LogTag.App, $"{items[i].GetType().Name}.Tick failed", e);
                }
            }
        }

        public void LateTick()
        {
            var items = _lateTickables.Snapshot();
            for (var i = 0; i < items.Length; i++)
            {
                try
                {
                    items[i].LateTick();
                }
                catch (Exception e)
                {
                    _log.Error(LogTag.App, $"{items[i].GetType().Name}.LateTick failed", e);
                }
            }
        }

        public void Pause(bool paused) => Each(l => l.OnApplicationPause(paused));
        public void Focus(bool focused) => Each(l => l.OnApplicationFocus(focused));
        public void Quit() => Each(l => l.OnApplicationQuit());
        public void LowMemory() => Each(l => l.OnLowMemory());

        private void Each(Action<IAppLifecycleListener> action)
        {
            foreach (var listener in _listeners.Snapshot())
            {
                try
                {
                    action(listener);
                }
                catch (Exception e)
                {
                    _log.Error(LogTag.App, $"{listener.GetType().Name} lifecycle callback failed", e);
                }
            }
        }

        private sealed class Registry<T> where T : class
        {
            private readonly List<T> _items = new List<T>();
            private T[] _snapshot = Array.Empty<T>();
            private bool _dirty;

            public int Count => _items.Count;

            public IDisposable Add(T item)
            {
                _items.Add(item);
                _dirty = true;
                return new Disposable(() =>
                {
                    _items.Remove(item);
                    _dirty = true;
                });
            }

            public T[] Snapshot()
            {
                if (!_dirty) return _snapshot;
                _snapshot = _items.ToArray();
                _dirty = false;
                return _snapshot;
            }
        }
    }
}
