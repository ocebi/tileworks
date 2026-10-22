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
    // The card before a level: its number, a Hard or Super Hard ribbon, and the goals, which fly into the HUD's goal chips
    // when it closes by itself or on a tap. ShowAsync completes once the goals have landed.
    public sealed class LevelIntroView : MonoBehaviour
    {
        [SerializeField, ReadOnlyInInspector] private UiMotion _uiMotion;
        [SerializeField, ReadOnlyInInspector] private LevelIntroConfig _config;
        [SerializeField, ReadOnlyInInspector] private CanvasGroup _group;
        [SerializeField, ReadOnlyInInspector] private RectTransform _card;
        [SerializeField, ReadOnlyInInspector] private Button _dismiss;
        [SerializeField, ReadOnlyInInspector] private TMP_Text _title;
        [SerializeField, ReadOnlyInInspector] private LevelBadgeSticker _badge;
        [SerializeField, ReadOnlyInInspector] private Image[] _icons = new Image[0];
        [SerializeField, ReadOnlyInInspector] private TMP_Text[] _counts = new TMP_Text[0];
        [SerializeField, ReadOnlyInInspector] private RectTransform _fliers;

        private readonly List<Image> _flying = new List<Image>();
        private readonly Stack<Image> _pool = new Stack<Image>();
        private IReadOnlyList<Image> _targets = Array.Empty<Image>();
        private int _goals;
        private LevelBadge _level;

        private void Awake()
        {
            _group.gameObject.SetActive(false);
        }

        // `targets` are the HUD's goal icons, already showing this level's goals; `counts` runs parallel to them.
        public async UniTask ShowAsync(int level, LevelBadge badge, IReadOnlyList<Image> targets, IReadOnlyList<int> counts, int goals, CancellationToken ct)
        {
            StopMotion();
            _targets = targets;
            _goals = Mathf.Min(goals, _icons.Length);
            _title.SetText("Level {0}", level);
            for (var i = 0; i < _icons.Length; i++)
            {
                var shown = i < _goals;
                _icons[i].transform.parent.gameObject.SetActive(shown);
                if (!shown) continue;
                _icons[i].sprite = targets[i].sprite;
                _icons[i].enabled = true;
                targets[i].enabled = false;
                _counts[i].SetText("{0}", counts[i]);
                _ = _uiMotion.PopIn(_icons[i].transform.parent, _uiMotion.StaggerDelay(i));
            }

            var hard = badge != LevelBadge.None;
            _level = badge;
            _badge.Show(LevelBadge.None, false);
            if (hard) _ = Tween.Delay(this, _config.BadgeDelay, view => view._badge.Show(view._level, true), useUnscaledTime: true);

            _group.gameObject.SetActive(true);
            _ = _uiMotion.Open(_group, _card);
            using (var hold = CancellationTokenSource.CreateLinkedTokenSource(ct))
            {
                await UniTask.WhenAny(_dismiss.OnClickAsync(hold.Token),
                    UniTask.Delay(TimeSpan.FromSeconds(hard ? _config.HardHold : _config.Hold), DelayType.UnscaledDeltaTime, cancellationToken: hold.Token));
                hold.Cancel();
            }

            await CloseAsync(ct);
        }

        // The goals fly to the HUD's chips; completes once the last one has landed.
        private async UniTask CloseAsync(CancellationToken ct)
        {
            Tween.StopAll(this);
            _ = _uiMotion.Close(_group, _card).OnComplete(_group, group => group.gameObject.SetActive(false));
            if (_uiMotion.Reduced || _goals == 0)
            {
                ShowTargets();
                return;
            }

            var config = _config;
            var last = 0f;
            for (var i = 0; i < _goals; i++)
            {
                var flier = Flier(_icons[i]);
                _icons[i].enabled = false;
                var target = _targets[i].rectTransform;
                var (from, to) = (flier.rectTransform.position, target.position);
                var d = to - from;
                var control = (from + to) * 0.5f + new Vector3(-d.y, d.x, 0f) * config.FlightArc;
                // Both canvases share one scaler, so canvas units compare directly.
                var endScale = target.rect.width / Mathf.Max(1f, flier.rectTransform.rect.width);
                var delay = i * config.FlightStagger;
                last = delay + config.Flight;
                var goal = i;
                _ = Tween.Custom(flier.rectTransform, 0f, 1f, config.Flight, (rect, t) =>
                    {
                        var u = 1f - t;
                        rect.position = u * u * from + 2f * u * t * control + t * t * to;
                        rect.localScale = Vector3.one * Mathf.Lerp(1f, endScale, t);
                    }, Ease.InOutSine, startDelay: delay, useUnscaledTime: true)
                    .OnComplete(this, view => view.Landed(goal, flier));
            }

            await UniTask.Delay(TimeSpan.FromSeconds(last), DelayType.UnscaledDeltaTime, cancellationToken: ct);
        }

        private void Landed(int goal, Image flier)
        {
            Release(flier);
            _targets[goal].enabled = true;
            _uiMotion.Punch(_targets[goal].transform.parent);
            _uiMotion.PlayStep(UiSound.GoalArrive, goal);
        }

        private Image Flier(Image source)
        {
            var flier = _pool.Count > 0 ? _pool.Pop() : CreateFlier();
            flier.gameObject.SetActive(true);
            flier.sprite = source.sprite;
            var rect = flier.rectTransform;
            rect.sizeDelta = source.rectTransform.rect.size;
            rect.position = source.rectTransform.position;
            rect.localScale = Vector3.one;
            _flying.Add(flier);
            return flier;
        }

        private Image CreateFlier()
        {
            var image = new GameObject("Flier", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            image.raycastTarget = false;
            image.preserveAspect = true;
            image.rectTransform.SetParent(_fliers, false);
            return image;
        }

        private void Release(Image flier)
        {
            Tween.StopAll(flier.rectTransform);
            flier.gameObject.SetActive(false);
            _flying.Remove(flier);
            _pool.Push(flier);
        }

        private void StopMotion()
        {
            Tween.StopAll(this);
            for (var i = _flying.Count - 1; i >= 0; i--) Release(_flying[i]);
            ShowTargets();
        }

        private void ShowTargets()
        {
            foreach (var target in _targets) target.enabled = true;
        }

        private void OnDestroy()
        {
            Tween.StopAll(this);
            foreach (var flier in _flying) Tween.StopAll(flier.rectTransform);
        }
    }
}
