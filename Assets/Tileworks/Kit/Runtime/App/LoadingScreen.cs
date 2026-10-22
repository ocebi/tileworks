using System;
using Tileworks.Kit.Unity;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Tileworks.Kit.App
{
    // Covers boot, slow scene changes and game switches. It has its own camera, so the screen never shows "no cameras".
    public sealed class LoadingScreen : MonoBehaviour, ILoadingOverlay, IProgress<float>
    {
        private const float ShowDelay = 0.2f;
        private const float FadeIn = 0.15f;
        private const float FadeOut = 0.25f;

        [SerializeField, ReadOnlyInInspector] private CanvasGroup _group;
        [SerializeField, ReadOnlyInInspector] private Camera _camera;
        [SerializeField, ReadOnlyInInspector, Tooltip("Left-pivoted; scaled by the fetch progress of a game's content.")] private RectTransform _bar;

        private bool _requested;
        private float _requestedAt;

        private void Awake() => Cover();

        // A quick transition never shows the screen; one that is already visible stays up.
        public void Show()
        {
            if (_requested) return;
            _requested = true;
            _requestedAt = _group.alpha > 0f ? float.NegativeInfinity : UnityEngine.Time.unscaledTime;
        }

        public void Hide() => _requested = false;

        // Shown only while content is fetched; a quick switch never shows it.
        public void Report(float value)
        {
            if (_bar == null) return;
            if (!_bar.gameObject.activeSelf) _bar.gameObject.SetActive(true);
            _bar.localScale = new Vector3(Mathf.Clamp01(value), 1f, 1f);
        }

        // Survives the unload of a game switch until the next game's own screen is loaded.
        public void CarryOver()
        {
            Cover();
            transform.SetParent(null);
            DontDestroyOnLoad(gameObject);
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void Cover()
        {
            _requested = true;
            _requestedAt = float.NegativeInfinity;
            _group.alpha = 1f;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode != LoadSceneMode.Single) return;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            Destroy(gameObject);
        }

        private void OnDestroy() => SceneManager.sceneLoaded -= OnSceneLoaded;

        private void Update()
        {
            var visible = _requested && UnityEngine.Time.unscaledTime - _requestedAt >= ShowDelay;
            var target = visible ? 1f : 0f;
            var alpha = Mathf.MoveTowards(_group.alpha, target, UnityEngine.Time.unscaledDeltaTime / (visible ? FadeIn : FadeOut));
            _group.alpha = alpha;
            _group.blocksRaycasts = alpha > 0f;
            _camera.enabled = _requested || alpha > 0f;
        }
    }
}
