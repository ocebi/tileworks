using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Tileworks.Kit.Unity
{
    public interface IPoolable
    {
        void OnSpawned();
        void OnDespawned();
    }

    public sealed class ViewPool<T> : IDisposable where T : Component
    {
        private readonly T _prefab;
        private readonly Transform _storage;
        private readonly Stack<T> _free = new Stack<T>();
        private readonly HashSet<T> _active = new HashSet<T>();

        public ViewPool(T prefab, Transform storage)
        {
            _prefab = prefab ? prefab : throw new ArgumentNullException(nameof(prefab));
            _storage = storage;
        }

        public int ActiveCount => _active.Count;
        public int FreeCount => _free.Count;

        public void Prewarm(int count)
        {
            for (var i = _free.Count + _active.Count; i < count; i++) _free.Push(Create());
        }

        public T Get(Transform parent)
        {
            var view = _free.Count > 0 ? _free.Pop() : Create();
            view.transform.SetParent(parent, false);
            view.gameObject.SetActive(true);
            _active.Add(view);
            if (view is IPoolable poolable) poolable.OnSpawned();
            return view;
        }

        public void Release(T view)
        {
            if (!_active.Remove(view)) return;
            if (view is IPoolable poolable) poolable.OnDespawned();
            view.gameObject.SetActive(false);
            view.transform.SetParent(_storage, false);
            _free.Push(view);
        }

        public void ReleaseAll()
        {
            foreach (var view in new List<T>(_active)) Release(view);
        }

        public void Dispose()
        {
            foreach (var view in _active) if (view) Object.Destroy(view.gameObject);
            foreach (var view in _free) if (view) Object.Destroy(view.gameObject);
            _active.Clear();
            _free.Clear();
        }

        private T Create()
        {
            var view = Object.Instantiate(_prefab, _storage);
            view.gameObject.SetActive(false);
            return view;
        }
    }
}
