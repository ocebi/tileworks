using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Tileworks.Kit.Content
{
    // Scenes from the player's scene list only, for an app built without a content system (tests, single scenes).
    public sealed class BuildSettingsContent : IContent
    {
        public UniTask InitializeAsync(CancellationToken ct) => UniTask.CompletedTask;

        public bool Exists(string key) => !string.IsNullOrEmpty(key) && Application.CanStreamedLevelBeLoaded(key);

        public UniTask<ContentHandle<T>> LoadAsync<T>(string key, CancellationToken ct) where T : UnityEngine.Object =>
            throw new NotSupportedException($"'{key}' cannot load: assets need the content system, this build has only its scene list.");

        public async UniTask<Scene> LoadSceneAsync(string key, LoadSceneMode mode, CancellationToken ct)
        {
            await SceneManager.LoadSceneAsync(key, mode).ToUniTask(cancellationToken: ct);
            return SceneManager.GetSceneByName(SceneKey.Of(key));
        }

        public async UniTask UnloadSceneAsync(Scene scene)
        {
            if (!scene.isLoaded) return;
            var unload = SceneManager.UnloadSceneAsync(scene);
            if (unload != null) await unload.ToUniTask();
        }

        public UniTask<long> DownloadSizeAsync(string key, CancellationToken ct) => UniTask.FromResult(0L);

        public UniTask DownloadAsync(string key, IProgress<float> progress, CancellationToken ct) => UniTask.CompletedTask;
    }
}
