using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Tileworks.Kit.Core;
using Tileworks.Kit.Unity;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Tileworks.Kit.Content.Addressables
{
    // The player's only built-in scene: fetches the first app scene's content, showing progress, then loads it.
    [DisallowMultipleComponent]
    public sealed class ContentEntry : MonoBehaviour, IProgress<float>
    {
        [SerializeField, ReadOnlyInInspector] private string _firstScene;
        [SerializeField, ReadOnlyInInspector] private Image _bar;

        private ILog _log;

        private void Start()
        {
            _log = new Log(LogLevel.Info);
            _log.AddSink(new UnityLogSink());
            EnterAsync(destroyCancellationToken).Forget(_log, "Content entry");
        }

        public void Report(float value)
        {
            if (_bar == null) return;
            if (!_bar.gameObject.activeSelf) _bar.gameObject.SetActive(true);
            _bar.fillAmount = Mathf.Clamp01(value);
        }

        private async UniTask EnterAsync(CancellationToken ct)
        {
            var started = DateTime.UtcNow;
            var content = new AddressablesContent();
            await content.InitializeAsync(ct);
            if (!content.Exists(_firstScene))
            {
                _log.Error(LogTag.Assets, $"First scene '{_firstScene}' is not in this build's content");
                return;
            }

            var bytes = await content.DownloadSizeAsync(_firstScene, ct);
            if (bytes > 0)
            {
                _log.Info(LogTag.Assets, "Fetching {0} KB for {1}", bytes / 1024, _firstScene);
                await content.DownloadAsync(_firstScene, this, ct);
            }

            _log.Info(LogTag.Assets, "Entering {0} after {1} ms", _firstScene, (int)(DateTime.UtcNow - started).TotalMilliseconds);
            await content.LoadSceneAsync(_firstScene, LoadSceneMode.Single, ct);
        }
    }
}
