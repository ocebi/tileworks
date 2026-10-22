using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;
using Addr = UnityEngine.AddressableAssets.Addressables;

namespace Tileworks.Kit.Content.Addressables
{
    // The only runtime code that talks to Addressables. A key it doesn't know falls back to the player's scene list.
    public sealed class AddressablesContent : IContent
    {
        private readonly Dictionary<Scene, AsyncOperationHandle<SceneInstance>> _scenes = new Dictionary<Scene, AsyncOperationHandle<SceneInstance>>();
        private bool _initialized;

        public async UniTask InitializeAsync(CancellationToken ct)
        {
            if (_initialized) return;
            var handle = Addr.InitializeAsync(false);
            try
            {
                await handle.ToUniTask(cancellationToken: ct);
            }
            finally
            {
                Addr.Release(handle);
            }

            _initialized = true;
        }

        public bool Exists(string key)
        {
            if (string.IsNullOrEmpty(key)) return false;
            return IsAddressable(SceneKey.Of(key)) || Application.CanStreamedLevelBeLoaded(key);
        }

        public async UniTask<ContentHandle<T>> LoadAsync<T>(string key, CancellationToken ct) where T : UnityEngine.Object
        {
            var handle = Addr.LoadAssetAsync<T>(key);
            try
            {
                await handle.ToUniTask(cancellationToken: ct);
            }
            catch
            {
                Addr.Release(handle);
                throw;
            }

            return new AddressableHandle<T>(handle);
        }

        public async UniTask<Scene> LoadSceneAsync(string key, LoadSceneMode mode, CancellationToken ct)
        {
            var name = SceneKey.Of(key);
            if (!IsAddressable(name))
            {
                await SceneManager.LoadSceneAsync(key, mode).ToUniTask(cancellationToken: ct);
                return SceneManager.GetSceneByName(name);
            }

            var handle = Addr.LoadSceneAsync(name, mode);
            var instance = await handle.ToUniTask(cancellationToken: ct);
            if (mode == LoadSceneMode.Additive) _scenes[instance.Scene] = handle;
            return instance.Scene;
        }

        public async UniTask UnloadSceneAsync(Scene scene)
        {
            // A scene gone with a single-mode load has released its handle already.
            if (_scenes.Remove(scene, out var handle))
            {
                if (scene.isLoaded && handle.IsValid()) await Addr.UnloadSceneAsync(handle).ToUniTask();
                return;
            }

            if (!scene.isLoaded) return;
            var unload = SceneManager.UnloadSceneAsync(scene);
            if (unload != null) await unload.ToUniTask();
        }

        public async UniTask<long> DownloadSizeAsync(string key, CancellationToken ct)
        {
            var name = SceneKey.Of(key);
            if (!IsAddressable(name)) return 0L;
            var handle = Addr.GetDownloadSizeAsync(name);
            try
            {
                return await handle.ToUniTask(cancellationToken: ct);
            }
            finally
            {
                Addr.Release(handle);
            }
        }

        public async UniTask DownloadAsync(string key, IProgress<float> progress, CancellationToken ct)
        {
            var name = SceneKey.Of(key);
            if (!IsAddressable(name)) return;
            var handle = Addr.DownloadDependenciesAsync(name, false);
            try
            {
                await handle.ToUniTask(progress, cancellationToken: ct);
            }
            finally
            {
                Addr.Release(handle);
            }
        }

        // Only valid after InitializeAsync: before it there are no locators, so nothing is addressable yet.
        private bool IsAddressable(string key)
        {
            if (!_initialized) return false;
            foreach (var locator in Addr.ResourceLocators)
            {
                if (locator.Locate(key, typeof(object), out _)) return true;
            }

            return false;
        }
    }
}
