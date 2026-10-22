using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Hub.Controller;
using PrimeTween;
using Tileworks.Kit.App;
using Tileworks.Kit.UI;
using Tileworks.Kit.Unity;
using UnityEngine;
using UnityEngine.UI;

namespace Hub.View
{
    public sealed class HubView : MonoBehaviour, IHubView
    {
        [Serializable]
        public sealed class Game
        {
            public string BootScene;
            public Sprite Backdrop;
            public GameObject Title;
            [Tooltip("Bottom-pivot parent that breathes; its first child pops in.")] public RectTransform Mascot;
            [Tooltip("Rises and grows when selected.")] public RectTransform Slot;
            public Button Card;
            public Image Glow;
            public RectTransform Badge;
            public RectTransform Name;
        }

        [SerializeField, ReadOnlyInInspector] private HubFeel _feel;
        [SerializeField, ReadOnlyInInspector] private Game[] _games = new Game[0];
        [SerializeField, ReadOnlyInInspector] private Image _back;
        [SerializeField, ReadOnlyInInspector] private Image _front;
        [SerializeField, ReadOnlyInInspector] private RectTransform _wordmark;
        [SerializeField, ReadOnlyInInspector] private CanvasGroup _controls;
        [SerializeField, ReadOnlyInInspector] private Button _play;
        [SerializeField, ReadOnlyInInspector] private RectTransform _playPulse;
        [SerializeField, ReadOnlyInInspector] private UiMotion _uiMotion;

        private readonly List<Game> _shown = new List<Game>();
        private AspectRatioFitter _backFit;
        private AspectRatioFitter _frontFit;
        private int _selected = -1;

        private IHubHandler _handler;

        private void Awake()
        {
            _backFit = _back.GetComponent<AspectRatioFitter>();
            _frontFit = _front.GetComponent<AspectRatioFitter>();
            _play.onClick.AddListener(() => _handler?.OnPlay());
            foreach (var game in _games)
            {
                var clicked = game;
                game.Card.onClick.AddListener(() => _handler?.OnCard(_shown.IndexOf(clicked)));
            }
        }

        public void Bind(IHubHandler handler) => _handler = handler;

        // A game without art keeps its index, so the presenter's indices still match.
        public void Open(IReadOnlyList<GameEntry> games, int selected)
        {
            _shown.Clear();
            foreach (var game in _games) Show(game, false);
            foreach (var entry in games)
            {
                var game = Array.Find(_games, g => g.BootScene == entry.BootScene);
                if (game != null) game.Slot.gameObject.SetActive(true);
                _shown.Add(game);
            }

            _play.interactable = games.Count > 0;
            if (games.Count == 0) return;
            _selected = selected;
            for (var i = 0; i < _shown.Count; i++) Emphasise(_shown[i], i == selected, true);
            var current = _shown[selected];
            if (current != null)
            {
                Show(current, true);
                Fit(_back, _backFit, current.Backdrop);
            }

            var step = 0;
            _uiMotion.PopIn(_wordmark, _uiMotion.StaggerDelay(step++));
            if (current != null)
            {
                _uiMotion.PopIn(current.Title.transform, _uiMotion.StaggerDelay(step++));
                _uiMotion.PopIn(current.Mascot.GetChild(0), _uiMotion.StaggerDelay(step++));
                _uiMotion.Breathe(current.Mascot);
            }

            // Each name pops in just after its card.
            foreach (var game in _shown)
            {
                if (game == null) continue;
                var delay = _uiMotion.StaggerDelay(step++);
                _uiMotion.PopIn(game.Card.transform, delay);
                _uiMotion.PopIn(game.Name, delay + _uiMotion.Config.Stagger);
            }

            _uiMotion.PopIn(_play.transform, _uiMotion.StaggerDelay(step));
            _uiMotion.Breathe(_playPulse);
        }

        public void Select(int index)
        {
            if (index == _selected) return;
            var previous = _shown[_selected];
            _selected = index;
            for (var i = 0; i < _shown.Count; i++) Emphasise(_shown[i], i == index, false);
            if (previous != null) Show(previous, false);
            var game = _shown[index];
            if (game != null)
            {
                Show(game, true);
                _uiMotion.PopIn(game.Title.transform, 0f);
                _uiMotion.PopIn(game.Mascot.GetChild(0), 0f);
                _uiMotion.Breathe(game.Mascot);
                Crossfade(game.Backdrop);
            }

            _uiMotion.Punch(_play.transform);
        }

        public void Nudge(int index)
        {
            if (_shown[index] != null) _uiMotion.Punch(_shown[index].Card.transform);
        }

        public async UniTask LeaveAsync()
        {
            _controls.interactable = false;
            _controls.blocksRaycasts = false;
            await Tween.Alpha(_controls, 0f, _feel.LeaveDuration, useUnscaledTime: true);
        }

        // Only the selected game's title and mascot are shown; its card keeps its slot either way.
        private static void Show(Game game, bool selected)
        {
            game.Title.SetActive(selected);
            game.Mascot.gameObject.SetActive(selected);
            if (!selected) UiMotion.StopBreathing(game.Mascot);
        }

        private void Emphasise(Game game, bool selected, bool instant)
        {
            if (game == null) return;
            Tween.StopAll(game.Slot);
            Tween.StopAll(game.Glow);
            var scale = Vector3.one * (selected ? _feel.SelectedScale : _feel.OtherScale);
            var position = new Vector2(game.Slot.anchoredPosition.x, selected ? _feel.SelectedLift : 0f);
            if (instant || _uiMotion.Reduced)
            {
                game.Slot.localScale = scale;
                game.Slot.anchoredPosition = position;
            }
            else
            {
                var duration = selected ? _feel.SelectDuration : _feel.OtherDuration;
                var ease = selected ? _feel.SelectEase : Ease.OutQuad;
                Tween.Scale(game.Slot, scale, duration, ease, useUnscaledTime: true);
                Tween.UIAnchoredPosition(game.Slot, position, duration, ease, useUnscaledTime: true);
            }

            game.Glow.enabled = selected;
            game.Badge.gameObject.SetActive(selected);
            if (!selected) return;
            if (!instant) _uiMotion.PopIn(game.Badge, 0f);
            if (_uiMotion.Reduced) game.Glow.color = Color.white;
            else
                Tween.Alpha(game.Glow, 1f, _feel.GlowLow, _feel.GlowPeriod * 0.5f, Ease.InOutSine, cycles: -1, cycleMode: CycleMode.Yoyo, useUnscaledTime: true);
        }

        // The front layer fades the new backdrop in over the old one, then hands it to the back layer and switches off.
        private void Crossfade(Sprite sprite)
        {
            Tween.StopAll(_front);
            if (_front.enabled) Fit(_back, _backFit, _front.sprite);
            Fit(_front, _frontFit, sprite);
            _front.enabled = true;
            Tween.Alpha(_front, 0f, 1f, _feel.Crossfade(_uiMotion.Reduced), useUnscaledTime: true).OnComplete(this, view => view.EndCrossfade());
        }

        private void EndCrossfade()
        {
            Fit(_back, _backFit, _front.sprite);
            _front.enabled = false;
        }

        private static void Fit(Image image, AspectRatioFitter fitter, Sprite sprite)
        {
            image.sprite = sprite;
            if (sprite != null) fitter.aspectRatio = sprite.rect.width / sprite.rect.height;
        }

        // Only stops tweens: on a scene unload the targets may already be destroyed, so nothing is reset.
        private void OnDestroy()
        {
            Tween.StopAll(_front);
            Tween.StopAll(_controls);
            Tween.StopAll(_playPulse);
            foreach (var game in _games)
            {
                Tween.StopAll(game.Slot);
                Tween.StopAll(game.Glow);
                Tween.StopAll(game.Mascot);
            }
        }
    }
}
