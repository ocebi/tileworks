using System.Threading;
using BlockPuzzle.Controller;
using Cysharp.Threading.Tasks;
using Tileworks.Kit.UI;
using Tileworks.Kit.Unity;
using Tileworks.Tiles.View;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BlockPuzzle.View
{
    // One popup panel reused for win, lose and pause; buttons are shown per mode.
    public sealed class PuzzleResultView : MonoBehaviour, IPuzzleResultView
    {
        [SerializeField, ReadOnlyInInspector] private UiMotion _uiMotion;
        [SerializeField, ReadOnlyInInspector] private CanvasGroup _group;
        [SerializeField, ReadOnlyInInspector] private RectTransform _panel;
        [SerializeField, ReadOnlyInInspector] private TMP_Text _title;
        [SerializeField, ReadOnlyInInspector] private TMP_Text _body;
        [SerializeField, ReadOnlyInInspector] private WinReveal _win;
        [SerializeField, ReadOnlyInInspector] private Button _next;
        [SerializeField, ReadOnlyInInspector] private Button _retry;
        [SerializeField, ReadOnlyInInspector] private Button _home;
        [SerializeField, ReadOnlyInInspector] private Button _levels;
        [SerializeField, ReadOnlyInInspector] private Button _continue;
        [SerializeField, ReadOnlyInInspector] private Button _resume;
        [SerializeField, ReadOnlyInInspector] private TMP_Text _continueLabel;
        [SerializeField, ReadOnlyInInspector] private GameObject _settingsRow;
        [SerializeField, ReadOnlyInInspector] private Button _sound;
        [SerializeField, ReadOnlyInInspector] private Button _music;
        [SerializeField, ReadOnlyInInspector] private Button _haptics;
        [SerializeField, ReadOnlyInInspector] private Button _motion;

        private PopupMotion _popup;
        private Button[] _actions;
        private TMP_Text _nextLabel;
        private int _replayOf;

        private ISettingsHandler _settings;

        private readonly TextNumbers _text = new TextNumbers();

        private void Awake()
        {
            _popup = new PopupMotion(_uiMotion, _group, _panel);
            _actions = new[] { _next, _continue, _resume, _retry, _home, _levels };
            _nextLabel = _next.GetComponentInChildren<TMP_Text>();
            _resume.onClick.AddListener(Hide);
            _sound.onClick.AddListener(() => _settings?.OnToggle(SettingToggle.Sound));
            _music.onClick.AddListener(() => _settings?.OnToggle(SettingToggle.Music));
            _haptics.onClick.AddListener(() => _settings?.OnToggle(SettingToggle.Haptics));
            _motion.onClick.AddListener(() => _settings?.OnToggle(SettingToggle.ReduceMotion));
            _popup.HideNow();
        }

        public void SetReplay(int currentLevel) => _replayOf = currentLevel;

        public UniTask<ResultChoice> ShowWin(int levelNumber, int coins, int score, CancellationToken ct)
        {
            var replay = _replayOf > 0;
            Prepare("Level Complete!", replay ? $"Replay cleared!\nYou're still on Level {_replayOf}." : string.Empty, next: true);
            if (replay) _text.Set(_nextLabel, "Play Level ", _replayOf);
            else _nextLabel.text = "Next Level";
            Open(_win.Play(levelNumber, score, replay ? 0 : coins, _uiMotion.StaggerDelay(1)));
            return ChoiceAsync(ct);
        }

        public UniTask<ResultChoice> ShowLose(bool canContinue, int continuePrice, bool canAfford, CancellationToken ct)
        {
            const string body = "No room for these pieces.";
            var more = _replayOf > 0 ? "\nReplays don't cost lives." : canContinue ? "\nContinue with new pieces?" : "\nTrying again costs a life.";
            Prepare("Out of Space", body + more, retry: true, home: true, cont: canContinue);
            _text.Set(_continueLabel, "Continue  ", continuePrice);
            _continue.interactable = canAfford;
            Open(_uiMotion.StaggerDelay(1));
            return ChoiceAsync(ct);
        }

        public UniTask<ResultChoice> ShowPause(CancellationToken ct)
        {
            Prepare("Paused", string.Empty, home: true, resume: true, retry: true, settings: true);
            Open(_uiMotion.StaggerDelay(1));
            return ChoiceAsync(ct);
        }

        // The first button pressed wins; cancelling the popup's token releases the others.
        private async UniTask<ResultChoice> ChoiceAsync(CancellationToken ct)
        {
            using var popup = CancellationTokenSource.CreateLinkedTokenSource(ct);
            var token = popup.Token;
            var choice = await UniTask.WhenAny(_next.OnClickAsync(token), _retry.OnClickAsync(token), _home.OnClickAsync(token), _levels.OnClickAsync(token),
                _continue.OnClickAsync(token), _resume.OnClickAsync(token));
            popup.Cancel();
            return (ResultChoice)choice;
        }

        public void SetSettings(bool sound, bool haptics, bool reduceMotion)
        {
            SetToggle(_sound, sound ? "Sound\nOn" : "Sound\nOff", sound);
            SetToggle(_haptics, haptics ? "Vibration\nOn" : "Vibration\nOff", haptics);
            SetToggle(_motion, reduceMotion ? "Motion\nReduced" : "Motion\nFull", !reduceMotion);
        }

        public bool IsOpen => _popup.IsOpen;

        public void Hide()
        {
            _popup.Close();
            _win.Stop();
        }

        public void SetMusic(bool music) => SetToggle(_music, music ? "Music\nOn" : "Music\nOff", music);

        public void Bind(ISettingsHandler handler) => _settings = handler;

        private void SetToggle(Button button, string label, bool on)
        {
            var text = button.GetComponentInChildren<TMP_Text>();
            var changed = text.text != label;
            text.text = label;
            button.targetGraphic.color = on ? Color.white : new Color(1f, 1f, 1f, 0.45f);
            if (changed && IsOpen) _uiMotion.Punch(button.transform);
        }

        private void Prepare(string title, string body, bool next = false, bool retry = false, bool home = false, bool cont = false, bool resume = false,
            bool settings = false)
        {
            _title.text = title;
            _body.text = body;
            _body.gameObject.SetActive(body.Length > 0);
            _next.gameObject.SetActive(next);
            _retry.gameObject.SetActive(retry);
            var replay = _replayOf > 0 && (next || retry) && !resume;
            _home.gameObject.SetActive((home || next) && !replay);
            _levels.gameObject.SetActive(replay);
            _continue.gameObject.SetActive(cont);
            _resume.gameObject.SetActive(resume);
            _settingsRow.SetActive(settings);
            _win.Hide();
        }

        // Opens the panel; the buttons pop in one after another from `actionsDelay`.
        private void Open(float actionsDelay)
        {
            _popup.Open();
            _uiMotion.PopIn(_title.transform, _uiMotion.StaggerDelay(0));
            var shown = 0;
            foreach (var action in _actions)
            {
                if (action.gameObject.activeSelf) _uiMotion.PopIn(action.transform, _uiMotion.Reduced ? 0f : actionsDelay + _uiMotion.Config.Stagger * shown++);
            }
        }
    }
}
