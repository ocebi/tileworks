using System;
using System.Collections.Generic;
using System.Threading;
using Match3.Controller;
using Match3.Model;
using Cysharp.Threading.Tasks;
using Tileworks.Kit.UI;
using Tileworks.Kit.Unity;
using Tileworks.Tiles.View;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Match3.View
{
    // One popup for win, out of moves and pause: a ribbon title, one content block per mode, then the active buttons
    // stacked under it, the first one large.
    public sealed class Match3ResultView : MonoBehaviour, IMatch3ResultView
    {
        [SerializeField, ReadOnlyInInspector] private UiMotion _uiMotion;
        [SerializeField, ReadOnlyInInspector] private Match3FeelConfig _feel;
        [SerializeField, ReadOnlyInInspector] private Match3Skin _skin;
        [SerializeField, ReadOnlyInInspector] private CanvasGroup _group;
        [SerializeField, ReadOnlyInInspector] private RectTransform _panel;
        [SerializeField, ReadOnlyInInspector] private Image _ribbon;
        [SerializeField, ReadOnlyInInspector] private Sprite _happyRibbon;
        [SerializeField, ReadOnlyInInspector] private Sprite _sadRibbon;
        [SerializeField, ReadOnlyInInspector] private TMP_Text _title;
        [SerializeField, ReadOnlyInInspector] private TMP_Text _body;
        [SerializeField, ReadOnlyInInspector] private WinReveal _win;
        [SerializeField, ReadOnlyInInspector] private GameObject _goalsRow;
        [SerializeField, ReadOnlyInInspector] private Image[] _goalIcons = new Image[0];
        [SerializeField, ReadOnlyInInspector] private TMP_Text[] _goalCounts = new TMP_Text[0];
        [SerializeField, ReadOnlyInInspector] private GameObject[] _goalChecks = new GameObject[0];
        [SerializeField, ReadOnlyInInspector] private GameObject _extraMoves;
        [SerializeField, ReadOnlyInInspector] private TMP_Text _extraMovesText;
        [SerializeField, ReadOnlyInInspector] private GameObject _settingsRow;
        [SerializeField, ReadOnlyInInspector] private Button _sound;
        [SerializeField, ReadOnlyInInspector] private Button _music;
        [SerializeField, ReadOnlyInInspector] private Button _haptics;
        [SerializeField, ReadOnlyInInspector] private Button _motion;
        [SerializeField, ReadOnlyInInspector] private Button _close;
        [SerializeField, ReadOnlyInInspector] private Button _next;
        [SerializeField, ReadOnlyInInspector] private Button _continue;
        [SerializeField, ReadOnlyInInspector] private TMP_Text _continuePrice;
        [SerializeField, ReadOnlyInInspector] private Button _resume;
        [SerializeField, ReadOnlyInInspector] private Button _retry;
        [SerializeField, ReadOnlyInInspector] private Button _home;
        [SerializeField, ReadOnlyInInspector] private Button _levels;
        [SerializeField, ReadOnlyInInspector] private Button _giveUp;

        private PopupMotion _popup;
        private Button[] _actions;
        private TMP_Text _nextLabel;
        private Vector2 _bodyAt;
        private int _replayOf;

        private ISettingsHandler _settings;

        private readonly TextNumbers _text = new TextNumbers();

        private void Awake()
        {
            _popup = new PopupMotion(_uiMotion, _group, _panel);
            _actions = new[] { _next, _continue, _resume, _retry, _home, _giveUp, _levels };
            _nextLabel = _next.GetComponentInChildren<TMP_Text>();
            _bodyAt = _body.rectTransform.anchoredPosition;
            _resume.onClick.AddListener(Hide);
            _close.onClick.AddListener(Hide);
            _sound.onClick.AddListener(() => _settings?.OnToggle(SettingToggle.Sound));
            _music.onClick.AddListener(() => _settings?.OnToggle(SettingToggle.Music));
            _haptics.onClick.AddListener(() => _settings?.OnToggle(SettingToggle.Haptics));
            _motion.onClick.AddListener(() => _settings?.OnToggle(SettingToggle.ReduceMotion));
            _popup.HideNow();
        }

        public bool IsOpen => _popup.IsOpen;

        public void SetReplay(int currentLevel) => _replayOf = currentLevel;

        // A replay's note goes under the reveal, and the buttons below it.
        public UniTask<ResultChoice> ShowWin(int levelNumber, int coins, CancellationToken ct)
        {
            var replay = _replayOf > 0;
            Begin("Level complete!", _happyRibbon, replay ? $"Replay cleared!\nYou're still on Level {_replayOf}." : string.Empty);
            if (replay) _text.Set(_nextLabel, "Play Level ", _replayOf);
            else _nextLabel.text = "Next level";
            var win = (RectTransform)_win.transform;
            var top = win.anchoredPosition.y - win.rect.height - _feel.ResultButtonGap;
            var delay = _win.Play(levelNumber, 0, replay ? 0 : coins, _uiMotion.StaggerDelay(1));
            if (replay)
            {
                var height = Mathf.Max(_body.rectTransform.rect.height, _body.preferredHeight);
                _body.rectTransform.anchoredPosition = new Vector2(_bodyAt.x, top + _feel.ResultButtonGap - height * 0.5f);
                Open(top - height, delay, _next, _levels);
            }
            else Open(top, delay, _next, _home);

            return ChoiceAsync(ct);
        }

        public UniTask<ResultChoice> ShowLose(IReadOnlyList<Match3Goal> goals, IReadOnlyList<int> goalsLeft, bool canContinue, int extraMoves, int continuePrice,
            bool canAfford, CancellationToken ct)
        {
            var replay = _replayOf > 0;
            Begin("Out of moves!", _sadRibbon, replay ? "So close. Replays don't cost lives.\nStill to do:" : "So close. Still to do:");
            _goalsRow.SetActive(true);
            for (var i = 0; i < _goalIcons.Length; i++)
            {
                var shown = i < goals.Count;
                _goalIcons[i].transform.parent.gameObject.SetActive(shown);
                if (!shown) continue;
                var done = goalsLeft[i] <= 0;
                _goalIcons[i].sprite = _skin.GoalSprite(goals[i]);
                _text.Set(_goalCounts[i], goalsLeft[i]);
                _goalCounts[i].gameObject.SetActive(!done);
                _goalChecks[i].SetActive(done);
            }

            _extraMoves.SetActive(canContinue);
            _text.Set(_extraMovesText, "+", extraMoves);
            _text.Set(_continuePrice, continuePrice);
            _continue.interactable = canAfford;
            if (canContinue) Open(-760f, _uiMotion.StaggerDelay(1), _continue, _retry, _giveUp);
            else Open(-560f, _uiMotion.StaggerDelay(1), _retry, replay ? _levels : _giveUp);
            return ChoiceAsync(ct);
        }

        public UniTask<ResultChoice> ShowPause(CancellationToken ct)
        {
            Begin("Paused", _happyRibbon, string.Empty);
            _settingsRow.SetActive(true);
            _close.gameObject.SetActive(true);
            Open(-560f, _uiMotion.StaggerDelay(1), _resume, _retry, _home);
            return ChoiceAsync(ct);
        }

        // The first button pressed wins; cancelling the popup's token releases the others. Give Up goes home; Close resumes.
        private async UniTask<ResultChoice> ChoiceAsync(CancellationToken ct)
        {
            using var popup = CancellationTokenSource.CreateLinkedTokenSource(ct);
            var token = popup.Token;
            var pressed = await UniTask.WhenAny(_next.OnClickAsync(token), _retry.OnClickAsync(token), _home.OnClickAsync(token), _levels.OnClickAsync(token),
                _continue.OnClickAsync(token), _resume.OnClickAsync(token), _giveUp.OnClickAsync(token), _close.OnClickAsync(token));
            popup.Cancel();
            return pressed switch
            {
                0 => ResultChoice.Next,
                1 => ResultChoice.Retry,
                2 => ResultChoice.Home,
                3 => ResultChoice.Levels,
                4 => ResultChoice.Continue,
                6 => ResultChoice.Home,
                _ => ResultChoice.Resume,
            };
        }

        public void SetSettings(bool sound, bool haptics, bool reduceMotion)
        {
            SetToggle(_sound, sound ? "Sound on" : "Sound off", sound);
            SetToggle(_haptics, haptics ? "Vibration on" : "Vibration off", haptics);
            SetToggle(_motion, reduceMotion ? "Motion reduced" : "Motion full", !reduceMotion);
        }

        public void Hide()
        {
            _popup.Close();
            _win.Stop();
        }

        private void Begin(string title, Sprite ribbon, string body)
        {
            _title.text = title;
            _ribbon.sprite = ribbon;
            _body.text = body;
            _body.gameObject.SetActive(body.Length > 0);
            _body.rectTransform.anchoredPosition = _bodyAt;
            _win.Hide();
            _goalsRow.SetActive(false);
            _extraMoves.SetActive(false);
            _settingsRow.SetActive(false);
            _close.gameObject.SetActive(false);
            foreach (var action in _actions) action.gameObject.SetActive(false);
        }

        // Stacks the buttons from `top` down and sizes the panel to end below the last one; they pop in from `actionsDelay`.
        private void Open(float top, float actionsDelay, params Button[] buttons)
        {
            var y = top;
            for (var i = 0; i < buttons.Length; i++)
            {
                var rect = (RectTransform)buttons[i].transform;
                buttons[i].gameObject.SetActive(true);
                rect.anchoredPosition = new Vector2(0f, y - rect.sizeDelta.y * 0.5f);
                y -= rect.sizeDelta.y + _feel.ResultButtonGap;
            }

            _panel.sizeDelta = new Vector2(_panel.sizeDelta.x, -y - _feel.ResultButtonGap + _feel.ResultBottomPadding);
            _popup.Open();
            _uiMotion.PopIn(_ribbon.transform, _uiMotion.StaggerDelay(0));
            for (var i = 0; i < buttons.Length; i++) _uiMotion.PopIn(buttons[i].transform, _uiMotion.Reduced ? 0f : actionsDelay + _uiMotion.Config.Stagger * i);
        }

        public void SetMusic(bool music) => SetToggle(_music, music ? "Music on" : "Music off", music);

        public void Bind(ISettingsHandler handler) => _settings = handler;

        private void SetToggle(Button button, string label, bool on)
        {
            var text = button.transform.parent.GetComponentInChildren<TMP_Text>();
            var changed = text.text != label;
            text.text = label;
            button.targetGraphic.color = on ? Color.white : new Color(1f, 1f, 1f, _feel.ToggleOffAlpha);
            if (changed && IsOpen) _uiMotion.Punch(button.transform);
        }
    }
}
