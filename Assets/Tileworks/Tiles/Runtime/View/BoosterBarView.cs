using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using PrimeTween;
using Tileworks.Kit.UI;
using Tileworks.Kit.Unity;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Tileworks.Tiles.View
{
    // Booster buttons with count badges, padlocks, a bubble when held, and a card that introduces a newly opened one. While a
    // booster is picked, the overlay dims the screen around the board and the picked button rises above it. A bar with no
    // overlay can't pick.
    public sealed class BoosterBarView : MonoBehaviour, IBoosterBarView, IBoosterPickHandler
    {
        [SerializeField, ReadOnlyInInspector] private UiMotion _uiMotion;
        [SerializeField, ReadOnlyInInspector] private BoosterBarConfig _config;
        [SerializeField, ReadOnlyInInspector] private string[] _names = new string[0];
        [SerializeField, ReadOnlyInInspector] private string[] _whats = new string[0];
        [SerializeField, ReadOnlyInInspector] private Button[] _buttons = new Button[0];
        [SerializeField, ReadOnlyInInspector] private RectTransform[] _pulses = new RectTransform[0];
        [SerializeField, ReadOnlyInInspector] private GameObject[] _art = new GameObject[0];
        [SerializeField, ReadOnlyInInspector] private GameObject[] _badges = new GameObject[0];
        [SerializeField, ReadOnlyInInspector] private TMP_Text[] _counts = new TMP_Text[0];
        [SerializeField, ReadOnlyInInspector] private GameObject[] _putBack = new GameObject[0];
        [SerializeField, ReadOnlyInInspector] private RectTransform[] _glows = new RectTransform[0];
        [SerializeField, ReadOnlyInInspector] private GameObject[] _locks = new GameObject[0];
        [SerializeField, ReadOnlyInInspector] private TMP_Text[] _lockLevels = new TMP_Text[0];
        [SerializeField, ReadOnlyInInspector] private HoldPress[] _holds = new HoldPress[0];
        [SerializeField, ReadOnlyInInspector] private RectTransform _bubble;
        [SerializeField, ReadOnlyInInspector] private TMP_Text _bubbleTitle;
        [SerializeField, ReadOnlyInInspector] private TMP_Text _bubbleBody;
        [SerializeField, ReadOnlyInInspector] private BoosterPickOverlay _overlay;
        [SerializeField, ReadOnlyInInspector] private CanvasGroup _intro;
        [SerializeField, ReadOnlyInInspector] private RectTransform _introCard;
        [SerializeField, ReadOnlyInInspector] private RectTransform _introFrame;
        [SerializeField, ReadOnlyInInspector] private TMP_Text _introTitle;
        [SerializeField, ReadOnlyInInspector] private TMP_Text _introBody;
        [SerializeField, ReadOnlyInInspector] private Button _introClose;

        private int[] _shown = new int[0];
        private int[] _lockAt = new int[0];
        private Vector2[] _homes = new Vector2[0];
        private Canvas[] _pulseCanvases = new Canvas[0];
        private GameObject _introArt;
        private Tween _bubbleTimer;
        private Sequence _pickMotion;
        private Sequence _introMotion;
        private Tween _introPop;
        private bool _interactable = true;
        private int _picked = -1;

        private IBoosterBarHandler _handler;

        private readonly TextNumbers _text = new TextNumbers();

        private void Awake()
        {
            var n = _buttons.Length;
            _shown = new int[n];
            _lockAt = new int[n];
            _homes = new Vector2[n];
            _pulseCanvases = new Canvas[n];
            for (var i = 0; i < n; i++)
            {
                var slot = i;
                _shown[i] = -1;
                _homes[i] = _pulses[i].anchoredPosition;
                _pulseCanvases[i] = _pulses[i].GetComponent<Canvas>();
                _buttons[i].onClick.AddListener(() => OnClick(slot));
                _holds[i].HoldSeconds = _config.HoldSeconds;
                _holds[i].Pressed += () => OnPressed(slot);
                _holds[i].Held += () => Explain(slot);
                _holds[i].Released += () =>
                {
                    if (!_bubbleTimer.isAlive) HideBubble();
                };
                _glows[i].gameObject.SetActive(false);
                _putBack[i].SetActive(false);
            }

            BoosterPickOverlay.Raise(_intro.GetComponent<Canvas>(), 2);
            if (_overlay != null) _overlay.Bind(this);
            _bubble.gameObject.SetActive(false);
            _intro.gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            _bubbleTimer.Stop();
            _pickMotion.Stop();
            _introMotion.Stop();
            _introPop.Stop();
            foreach (var pulse in _pulses)
                if (pulse != null) Tween.StopAll(pulse);
            foreach (var glow in _glows)
                if (glow != null) Tween.StopAll(glow);
        }

        public void SetCount(int slot, int count)
        {
            if (!Has(slot)) return;
            _text.Set(_counts[slot], count);
            var shown = _shown[slot];
            _shown[slot] = count;
            Refresh(slot);
            if (shown >= 0 && shown != count && _badges[slot].activeSelf) _uiMotion.Punch(_badges[slot].transform);
        }

        public void SetLock(int slot, int unlockLevel)
        {
            if (!Has(slot)) return;
            _lockAt[slot] = unlockLevel;
            if (unlockLevel > 0) _text.Set(_lockLevels[slot], "Lv ", unlockLevel);
            else _lockLevels[slot].text = string.Empty;
            UiMotion.StopBreathing(_pulses[slot]);
            Refresh(slot);
        }

        public void SetInteractable(bool interactable) => _interactable = interactable;

        public void Bind(IBoosterBarHandler handler) => _handler = handler;

        void IBoosterPickHandler.OnPickCancelled() => _handler?.OnCancel();

        // One card after another; the boosters just introduced start breathing once the last card has closed.
        public async UniTask IntroduceAsync(IReadOnlyList<int> slots, CancellationToken ct)
        {
            if (slots.Count == 0) return;
            // Finished first: the card can't be restarted while it is part of a running open or close.
            _introMotion.Complete();
            _intro.gameObject.SetActive(true);
            _introMotion = _uiMotion.Open(_intro, _introCard);
            for (var i = 0; i < slots.Count; i++)
            {
                if (!Has(slots[i])) continue;
                ShowIntro(slots[i]);
                if (i > 0) _introPop = _uiMotion.PopIn(_introCard, 0f);
                await _introClose.OnClickAsync(ct);
            }

            _introMotion.Complete();
            _introPop.Complete();
            await _uiMotion.Close(_intro, _introCard);
            _intro.gameObject.SetActive(false);
            foreach (var slot in slots)
            {
                if (Has(slot)) _ = _uiMotion.Breathe(_pulses[slot]);
            }
        }

        public void SetPicked(int slot, Rect boardScreen)
        {
            slot = Has(slot) ? slot : -1;
            if (slot == _picked) return;
            if (slot >= 0 && _overlay == null) throw new InvalidOperationException($"{name} has no BoosterPickOverlay, so it can't pick slot {slot}.");
            if (_picked >= 0) Drop(_picked);
            _picked = slot;
            HideBubble();
            if (slot < 0)
            {
                _overlay.Hide();
                return;
            }

            _overlay.Show(slot, _names[slot], boardScreen);
            Lift(slot);
        }

        public Vector2 ButtonScreenPoint(int slot)
        {
            if (!Has(slot)) return Vector2.zero;
            var root = _pulses[slot].GetComponentInParent<Canvas>().rootCanvas;
            var camera = root.renderMode == RenderMode.ScreenSpaceOverlay ? null : root.worldCamera;
            return RectTransformUtility.WorldToScreenPoint(camera, _pulses[slot].position);
        }

        // Worked out from the canvas size and the canvas scaler's own formula, so it holds before the canvas has laid itself out.
        public float ScreenTop
        {
            get
            {
                var root = GetComponentInParent<Canvas>().rootCanvas;
                var scale = root.scaleFactor;
                if (root.TryGetComponent<CanvasScaler>(out var scaler) && scaler.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize)
                {
                    var reference = scaler.referenceResolution;
                    var size = root.pixelRect.size;
                    var log = Mathf.Lerp(Mathf.Log(size.x / reference.x, 2f), Mathf.Log(size.y / reference.y, 2f), scaler.matchWidthOrHeight);
                    scale = Mathf.Pow(2f, log);
                }

                var raised = ((RectTransform)transform).anchoredPosition.y + _pulses[0].sizeDelta.y * 0.5f + _config.PickLift;
                return Screen.safeArea.yMin + raised * scale;
            }
        }

        private bool Has(int slot) => slot >= 0 && slot < _buttons.Length;

        private void Refresh(int slot)
        {
            var locked = _lockAt[slot] > 0;
            _locks[slot].SetActive(locked);
            _art[slot].SetActive(!locked);
            _badges[slot].SetActive(!locked && slot != _picked);
            _buttons[slot].interactable = !locked && _shown[slot] > 0 || slot == _picked;
        }

        private void OnClick(int slot)
        {
            if (_holds[slot].ConsumeHold() || !_interactable && slot != _picked) return;
            UiMotion.StopBreathing(_pulses[slot]);
            _handler?.OnClicked(slot);
        }

        // A booster that can't be used explains itself on a plain tap, and the bubble stays a moment.
        private void OnPressed(int slot)
        {
            if (slot == _picked || _lockAt[slot] <= 0 && _shown[slot] != 0) return;
            Explain(slot);
            _bubbleTimer.Stop();
            _bubbleTimer = Tween.Delay(this, _config.BubbleSeconds, view => view.HideBubble(), useUnscaledTime: true);
        }

        private void Explain(int slot)
        {
            if (slot == _picked) return;
            _bubbleTitle.text = _names[slot];
            if (_lockAt[slot] > 0) _text.Clear().Append("Unlocks at level ").Append(_lockAt[slot]).Append('.').Apply(_bubbleBody);
            else _text.Clear().Append(_whats[slot]).Append(_shown[slot] == 0 ? " None left." : string.Empty).Apply(_bubbleBody);
            var room = Mathf.Max(0f, ((RectTransform)_bubble.parent).rect.width * 0.5f - _bubble.rect.width * 0.5f - 16f);
            _bubble.anchoredPosition = new Vector2(Mathf.Clamp(_homes[slot].x, -room, room), _bubble.anchoredPosition.y);
            if (_bubble.gameObject.activeSelf) return;
            _bubble.gameObject.SetActive(true);
            _uiMotion.PopIn(_bubble, 0f);
        }

        private void HideBubble()
        {
            _bubbleTimer.Stop();
            _bubble.gameObject.SetActive(false);
        }

        private void Lift(int slot)
        {
            var pulse = _pulses[slot];
            UiMotion.StopBreathing(pulse);
            BoosterPickOverlay.Raise(_pulseCanvases[slot], 1);
            _putBack[slot].SetActive(true);
            _glows[slot].gameObject.SetActive(true);
            _uiMotion.Breathe(_glows[slot]);
            Refresh(slot);
            _pickMotion.Stop();
            _pickMotion = Sequence.Create(useUnscaledTime: true)
                .Group(Tween.UIAnchoredPositionY(pulse, _homes[slot].y + _config.PickLift, Timed(_config.PickDuration), Ease.OutBack))
                .Group(Tween.Scale(pulse, _config.PickScale, Timed(_config.PickDuration), Ease.OutBack));
        }

        private void Drop(int slot)
        {
            var pulse = _pulses[slot];
            _pickMotion.Stop();
            UiMotion.StopBreathing(_glows[slot]);
            _glows[slot].gameObject.SetActive(false);
            _putBack[slot].SetActive(false);
            _pulseCanvases[slot].overrideSorting = false;
            _picked = -1;
            Refresh(slot);
            _pickMotion = Sequence.Create(useUnscaledTime: true)
                .Group(Tween.UIAnchoredPositionY(pulse, _homes[slot].y, Timed(_config.PickDuration), Ease.OutQuad))
                .Group(Tween.Scale(pulse, 1f, Timed(_config.PickDuration), Ease.OutQuad));
        }

        private float Timed(float seconds) => _uiMotion.Reduced ? 0f : seconds;

        private void ShowIntro(int slot)
        {
            if (_introArt != null) Destroy(_introArt);
            _introArt = Instantiate(_art[slot], _introFrame, false);
            _introArt.SetActive(true);
            _introArt.transform.localScale = Vector3.one * (_introFrame.rect.width / Mathf.Max(1f, ((RectTransform)_buttons[slot].transform).rect.width));
            _introTitle.text = _names[slot];
            _introBody.text = _whats[slot];
        }
    }
}
