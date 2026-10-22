using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Tileworks.Kit.Content;
using Tileworks.Kit.Core;
using Tileworks.Kit.Injection;
using Tileworks.Kit.Unity;
using UnityEngine.SceneManagement;

namespace Tileworks.Kit.App
{
    public sealed class SceneHandle
    {
        public SceneHandle(string name, Scene scene, Scope scope)
        {
            Name = name;
            Scene = scene;
            Scope = scope;
        }

        public string Name { get; }
        public Scene Scene { get; }
        public Scope Scope { get; }
    }

    public interface ISceneLoader
    {
        UniTask<SceneHandle> LoadAsync(string sceneName, CancellationToken ct, Scope parent = null);
        UniTask UnloadAsync(SceneHandle handle);
    }

    // Loads scenes additively and builds each scene's scope as a child of the given parent (App by default).
    public sealed class SceneLoader : ISceneLoader, IDisposable
    {
        private readonly ILog _log;
        private readonly IContent _content;
        private readonly List<SceneHandle> _loaded = new List<SceneHandle>();
        private Scope _root;

        public SceneLoader(ILog log, IContent content)
        {
            _log = Guard.NotNull(log, nameof(log));
            _content = Guard.NotNull(content, nameof(content));
        }

        public IReadOnlyList<SceneHandle> Loaded => _loaded;

        public void AttachRoot(Scope root) => _root = root;

        public async UniTask<SceneHandle> LoadAsync(string sceneName, CancellationToken ct, Scope parent = null)
        {
            var started = DateTime.UtcNow;
            var scene = await _content.LoadSceneAsync(sceneName, LoadSceneMode.Additive, ct);
            SceneManager.SetActiveScene(scene);

            var parentScope = parent ?? _root ?? throw new InvalidOperationException("SceneLoader has no root scope.");
            SceneRoot sceneRoot = null;
            foreach (var go in scene.GetRootGameObjects())
            {
                if (go.TryGetComponent(out sceneRoot)) break;
            }

            var scope = sceneRoot != null ? sceneRoot.BuildScope(parentScope, sceneName) : parentScope.CreateChild(sceneName, null);
            var handle = new SceneHandle(sceneName, scene, scope);
            _loaded.Add(handle);
            await scope.StartAsync(ct);
            _log.Info(LogTag.App, "Loaded scene {0} in {1} ms", sceneName, (int)(DateTime.UtcNow - started).TotalMilliseconds);
            return handle;
        }

        public async UniTask UnloadAsync(SceneHandle handle)
        {
            if (handle == null || !_loaded.Remove(handle)) return;
            handle.Scope.Dispose();
            await _content.UnloadSceneAsync(handle.Scene);
        }

        public void Dispose()
        {
            for (var i = _loaded.Count - 1; i >= 0; i--)
            {
                var handle = _loaded[i];
                handle.Scope.Dispose();
                _content.UnloadSceneAsync(handle.Scene).Forget(_log, "Scene unload");
            }

            _loaded.Clear();
        }
    }
}
