using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Tileworks.Kit.Content;
using UnityEngine.SceneManagement;

namespace Tileworks.Kit.Tests
{
    // Content for tests: keys are declared, loads hand out the declared objects, nothing touches the scene manager.
    public sealed class FakeContent : IContent
    {
        public readonly HashSet<string> Keys = new HashSet<string>();
        public readonly Dictionary<string, UnityEngine.Object> Assets = new Dictionary<string, UnityEngine.Object>();
        public readonly Dictionary<string, long> Sizes = new Dictionary<string, long>();
        public readonly List<string> Downloaded = new List<string>();
        public readonly List<string> LoadedScenes = new List<string>();
        public int Initializations;
        public int LiveHandles;

        public UniTask InitializeAsync(CancellationToken ct)
        {
            Initializations++;
            return UniTask.CompletedTask;
        }

        public bool Exists(string key) => Keys.Contains(SceneKey.Of(key));

        public UniTask<ContentHandle<T>> LoadAsync<T>(string key, CancellationToken ct) where T : UnityEngine.Object
        {
            if (!Assets.TryGetValue(key, out var asset)) throw new KeyNotFoundException(key);
            LiveHandles++;
            return UniTask.FromResult<ContentHandle<T>>(new Handle<T>((T)asset, this));
        }

        public UniTask<Scene> LoadSceneAsync(string key, LoadSceneMode mode, CancellationToken ct)
        {
            LoadedScenes.Add(SceneKey.Of(key));
            return UniTask.FromResult(default(Scene));
        }

        public UniTask UnloadSceneAsync(Scene scene) => UniTask.CompletedTask;

        public UniTask<long> DownloadSizeAsync(string key, CancellationToken ct) =>
            UniTask.FromResult(Sizes.TryGetValue(SceneKey.Of(key), out var size) ? size : 0L);

        public UniTask DownloadAsync(string key, IProgress<float> progress, CancellationToken ct)
        {
            Downloaded.Add(SceneKey.Of(key));
            progress?.Report(1f);
            return UniTask.CompletedTask;
        }

        private sealed class Handle<T> : ContentHandle<T> where T : UnityEngine.Object
        {
            private FakeContent _owner;

            public Handle(T asset, FakeContent owner) : base(asset) => _owner = owner;

            public override void Dispose()
            {
                if (_owner == null) return;
                _owner.LiveHandles--;
                _owner = null;
            }
        }
    }
}
