using System;
using System.Collections.Generic;
using System.Threading;
using Match3.Controller;
using Match3.Model;
using Cysharp.Threading.Tasks;
using PrimeTween;
using Tileworks.Kit.UI;
using Tileworks.Kit.Unity;
using Tileworks.Tiles.View;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Match3.View
{
    // Moves card, goals card with up to four chips, pause pill, cascade callouts and the No moves ribbon. The counters sit on
    // their own nested canvas, and so do the goal pieces flying up from the board; a goal's count shows what has landed, with a
    // sparkle and a rising note. The moves counter pulses red when few are left.
    public sealed class Match3HudView : MonoBehaviour, IMatch3HudView
    {
        [SerializeField, ReadOnlyInInspector] private UiMotion _uiMotion;
        [SerializeField, ReadOnlyInInspector] private Match3Skin _skin;
        [SerializeField, ReadOnlyInInspector] private TMP_Text _moves;
        [SerializeField, ReadOnlyInInspector] private Image[] _goalIcons = new Image[0];
        [SerializeField, ReadOnlyInInspector] private TMP_Text[] _goalCounts = new TMP_Text[0];
        [SerializeField, ReadOnlyInInspector] private GameObject[] _goalChecks = new GameObject[0];
        [SerializeField, ReadOnlyInInspector] private Button _pause;
        [SerializeField, ReadOnlyInInspector] private GameObject _replayTag;
        [SerializeField, ReadOnlyInInspector] private Match3FeelConfig _feel;
        [SerializeField, ReadOnlyInInspector] private TMP_Text _callout;
        [SerializeField, ReadOnlyInInspector] private RectTransform _noMoves;
        [SerializeField, ReadOnlyInInspector] private UiBurst _goalSparkle;
        [SerializeField, ReadOnlyInInspector] private UiBurst _goalTrail;
        [SerializeField, ReadOnlyInInspector] private LevelIntroView _intro;
        [SerializeField, ReadOnlyInInspector] private RectTransform _bonus;

        private readonly Stack<Image> _flierPool = new Stack<Image>();
        private readonly List<Image> _flying = new List<Image>();
        private RectTransform _fliers;
        private int[] _left = new int[0];
        private int[] _shown = new int[0];
        private int[] _inFlight = new int[0];
        private int[] _displayed = new int[0];
        private int _goalCount;
        private int _movesShown = -1;
        private Vector2 _calloutHome;
        private Color _movesCalm;
        private int _arrivals;
        private float _lastArrival = -1f;

        private IMatch3HudHandler _handler;

        private readonly TextNumbers _text = new TextNumbers();

        private void Awake()
        {
            _pause.onClick.AddListener(() => _handler?.OnPause());
            _fliers = (RectTransform)new GameObject("Fliers", typeof(RectTransform), typeof(Canvas)).transform;
            _fliers.SetParent(transform, false);
            (_fliers.anchorMin, _fliers.anchorMax, _fliers.offsetMin, _fliers.offsetMax) = (Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            _calloutHome = ((RectTransform)_callout.transform).anchoredPosition;
            _movesCalm = _moves.color;
            _callout.gameObject.SetActive(false);
            _noMoves.gameObject.SetActive(false);
        }

        public void SetGoals(IReadOnlyList<Match3Goal> goals)
        {
            for (var i = _flying.Count - 1; i >= 0; i--) Release(_flying[i]);
            (_left, _shown, _inFlight, _displayed) = (new int[_goalIcons.Length], new int[_goalIcons.Length], new int[_goalIcons.Length], new int[_goalIcons.Length]);
            for (var i = 0; i < _goalIcons.Length; i++)
            {
                var shown = i < goals.Count;
                _goalIcons[i].transform.parent.gameObject.SetActive(shown);
                if (!shown) continue;
                _goalIcons[i].sprite = _skin.GoalSprite(goals[i]);
                _left[i] = _shown[i] = _displayed[i] = goals[i].Count;
                _text.Set(_goalCounts[i], goals[i].Count);
                _goalCounts[i].gameObject.SetActive(true);
                _goalChecks[i].SetActive(false);
            }

            _goalCount = Mathf.Min(goals.Count, _goalIcons.Length);
            _movesShown = -1;
            _uiMotion.Alarm(_moves, _movesCalm, false);
        }

        public void SetGoalLeft(int goal, int left)
        {
            if (goal < 0 || goal >= _goalCounts.Length) return;
            _left[goal] = Mathf.Min(_left[goal], left);
            if (_inFlight[goal] > 0) return;
            _shown[goal] = _left[goal];
            ShowGoal(goal);
        }

        public void Collect(int goal, int left, Sprite sprite, Vector2 screen, float screenSize, in GoalFlight flight)
        {
            if (goal < 0 || goal >= _goalIcons.Length) return;
            if (_uiMotion.Reduced)
            {
                SetGoalLeft(goal, left);
                return;
            }

            _left[goal] = Mathf.Min(_left[goal], left);
            _inFlight[goal]++;
            var flier = Flier(sprite);
            var rect = flier.rectTransform;
            var icon = _goalIcons[goal].rectTransform;
            rect.sizeDelta = icon.rect.size;
            rect.position = screen;
            var start = screenSize / Mathf.Max(1f, icon.rect.width * icon.lossyScale.x);
            var lifted = start * flight.LiftScale;
            rect.localScale = Vector3.one * start;

            // A quadratic curve that rises before it turns towards the goal.
            var (from, to) = ((Vector3)screen, icon.position);
            var d = to - from;
            var bend = new Vector3(-d.y, d.x, 0f) * (d.x >= 0f ? flight.Arc : -flight.Arc);
            var control = (from + to) * 0.5f + bend;
            Tween.Scale(rect, lifted, flight.Lift, Ease.OutQuad, useUnscaledTime: true);
            Tween.Custom(rect, 0f, 1f, flight.Duration, (r, t) =>
                {
                    var u = 1f - t;
                    r.position = u * u * from + 2f * u * t * control + t * t * to;
                    r.localScale = Vector3.one * Mathf.Lerp(lifted, 1f, t);
                    _goalTrail.Trail(r.position);
                }, Ease.InSine, startDelay: flight.Lift, useUnscaledTime: true)
                .OnComplete(this, view => view.Land(goal, flier));
        }

        private void OnDestroy()
        {
            foreach (var flier in _flying) Tween.StopAll(flier.rectTransform);
            Tween.StopAll(_callout.transform);
            Tween.StopAll(_callout);
            Tween.StopAll(_noMoves);
            Tween.StopAll(_moves.transform);
            Tween.StopAll(_bonus);
        }

        private void Land(int goal, Image flier)
        {
            Release(flier);
            _inFlight[goal]--;
            _shown[goal] = _inFlight[goal] == 0 ? _left[goal] : Mathf.Max(_left[goal], _shown[goal] - 1);
            ShowGoal(goal);
        }

        private void ShowGoal(int goal)
        {
            if (_displayed[goal] == _shown[goal]) return;
            _displayed[goal] = _shown[goal];
            var done = _shown[goal] <= 0;
            var wasDone = _goalChecks[goal].activeSelf;
            _text.Set(_goalCounts[goal], _shown[goal]);
            _goalCounts[goal].gameObject.SetActive(!done);
            _goalChecks[goal].SetActive(done);
            _uiMotion.Punch(_goalIcons[goal].transform.parent);
            _goalSparkle.Burst(_goalIcons[goal].transform.position);
            if (Time.unscaledTime - _lastArrival > _uiMotion.Config.StepReset) _arrivals = 0;
            _lastArrival = Time.unscaledTime;
            _uiMotion.PlayStep(UiSound.GoalArrive, _arrivals++);
            if (!done || wasDone) return;
            _uiMotion.PopIn(_goalChecks[goal].transform, 0f);
            _uiMotion.Play(UiSound.GoalDone);
        }

        private Image Flier(Sprite sprite)
        {
            Image flier;
            if (_flierPool.Count > 0)
            {
                flier = _flierPool.Pop();
                flier.gameObject.SetActive(true);
            }
            else
            {
                flier = new GameObject("Flier", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                flier.raycastTarget = false;
                flier.preserveAspect = true;
                flier.rectTransform.SetParent(_fliers, false);
            }

            flier.sprite = sprite;
            _flying.Add(flier);
            return flier;
        }

        private void Release(Image flier)
        {
            Tween.StopAll(flier.rectTransform);
            flier.gameObject.SetActive(false);
            _flying.Remove(flier);
            _flierPool.Push(flier);
        }

        public void SetMoves(int moves)
        {
            _text.Set(_moves, moves);
            var changed = _movesShown >= 0 && moves != _movesShown;
            if (moves <= _uiMotion.Config.LowMoves)
            {
                _uiMotion.Alarm(_moves, _movesCalm, true);
                if (changed && moves < _movesShown) _uiMotion.Play(UiSound.MovesLow);
            }
            else
            {
                _uiMotion.Alarm(_moves, _movesCalm, false);
                if (changed) _uiMotion.Punch(_moves.transform);
            }

            _movesShown = moves;
        }

        public void SetInteractable(bool interactable) => _pause.interactable = interactable;

        public void SetReplay(bool replay) => _replayTag.SetActive(replay);

        public void Bind(IMatch3HudHandler handler) => _handler = handler;

        public UniTask ShowIntro(int level, LevelBadge badge, CancellationToken ct) => _intro.ShowAsync(level, badge, _goalIcons, _left, _goalCount, ct);

        public float ShowBonus()
        {
            _uiMotion.Play(UiSound.Callout);
            return _uiMotion.Banner(_bonus);
        }

        public void ShowCascade(int count) =>
            _uiMotion.Callout(_callout, count >= 4 ? "Amazing!" : count == 3 ? "Great!" : "Nice!", _calloutHome);

        public void ShowNoMoves()
        {
            Tween.StopAll(_noMoves);
            _noMoves.gameObject.SetActive(true);
            _noMoves.localScale = Vector3.zero;
            Tween.Scale(_noMoves, 1f, _feel.BannerIn, Ease.OutBack, useUnscaledTime: true);
            Tween.Scale(_noMoves, 0f, _feel.BannerOut, Ease.InBack, startDelay: _feel.BannerIn + _feel.BannerHold, useUnscaledTime: true)
                .OnComplete(_noMoves, n => n.gameObject.SetActive(false));
        }
    }
}
