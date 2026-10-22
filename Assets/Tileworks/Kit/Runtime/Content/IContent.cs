using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine.SceneManagement;

namespace Tileworks.Kit.Content
{
    // Where scenes and assets come from. Scene keys are scene names; a build-settings path is accepted too.
    public interface IContent
    {
        UniTask InitializeAsync(CancellationToken ct);

        // True when this build can load the key.
        bool Exists(string key);

        // The handle keeps the asset loaded; disposing it lets the content system release it.
        UniTask<ContentHandle<T>> LoadAsync<T>(string key, CancellationToken ct) where T : UnityEngine.Object;

        UniTask<Scene> LoadSceneAsync(string key, LoadSceneMode mode, CancellationToken ct);
        UniTask UnloadSceneAsync(Scene scene);

        // Bytes still to fetch before the key and everything it needs are local; 0 when cached or shipped with the app.
        UniTask<long> DownloadSizeAsync(string key, CancellationToken ct);
        UniTask DownloadAsync(string key, IProgress<float> progress, CancellationToken ct);
    }
}
