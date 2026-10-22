using Cysharp.Threading.Tasks;
using Tileworks.Kit.UI;
using Tileworks.Kit.Unity;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Tileworks.Tiles.View
{
    // The Home's settings popup: the four toggles and Reset progress, which asks first. Toggles off are dimmed.
    public sealed class SettingsView : MonoBehaviour, ISettingsPanel
    {
        [SerializeField, ReadOnlyInInspector] private UiMotion _uiMotion;
        [SerializeField, ReadOnlyInInspector] private CanvasGroup _group;
        [SerializeField, ReadOnlyInInspector] private RectTransform _panel;
        [SerializeField, ReadOnlyInInspector] private Button[] _openers = new Button[0];
        [SerializeField, ReadOnlyInInspector] private Button _close;
        [SerializeField, ReadOnlyInInspector] private Button _sound;
        [SerializeField, ReadOnlyInInspector] private Button _music;
        [SerializeField, ReadOnlyInInspector] private Button _haptics;
        [SerializeField, ReadOnlyInInspector] private Button _motion;
        [SerializeField, ReadOnlyInInspector] private Button _reset;
        [SerializeField, ReadOnlyInInspector] private ConfirmPopup _confirm;

        private PopupMotion _popup;
        private ISettingsHandler _handler;
        private bool _asking;

        public bool IsOpen => _popup.IsOpen;

        private void Awake()
        {
            _popup = new PopupMotion(_uiMotion, _group, _panel);
            _popup.HideNow();
            foreach (var opener in _openers) opener.onClick.AddListener(Open);
            _close.onClick.AddListener(Close);
            _sound.onClick.AddListener(() => _handler?.OnToggle(SettingToggle.Sound));
            _music.onClick.AddListener(() => _handler?.OnToggle(SettingToggle.Music));
            _haptics.onClick.AddListener(() => _handler?.OnToggle(SettingToggle.Haptics));
            _motion.onClick.AddListener(() => _handler?.OnToggle(SettingToggle.ReduceMotion));
            _reset.onClick.AddListener(() => AskReset().Forget());
        }

        public void Bind(ISettingsHandler handler) => _handler = handler;

        public void SetSettings(bool sound, bool haptics, bool reduceMotion)
        {
            SetToggle(_sound, sound ? "Sound on" : "Sound off", sound);
            SetToggle(_haptics, haptics ? "Vibration on" : "Vibration off", haptics);
            SetToggle(_motion, reduceMotion ? "Motion reduced" : "Motion full", !reduceMotion);
        }

        public void SetMusic(bool music) => SetToggle(_music, music ? "Music on" : "Music off", music);

        public void Open() => _popup.Open();

        public void Close() => _popup.Close();

        private async UniTaskVoid AskReset()
        {
            if (_asking || _handler == null) return;
            _asking = true;
            try
            {
                var yes = await _confirm.AskAsync("Reset progress?", "This game's levels, coins and boosters start over.", "Reset",
                    this.GetCancellationTokenOnDestroy());
                if (yes) _handler?.OnResetProgress();
            }
            finally
            {
                _asking = false;
            }
        }

        private void SetToggle(Button button, string label, bool on)
        {
            var text = button.transform.parent.GetComponentInChildren<TMP_Text>();
            var changed = text.text != label;
            text.text = label;
            button.targetGraphic.color = on ? Color.white : new Color(1f, 1f, 1f, 0.5f);
            if (changed && IsOpen) _uiMotion.Punch(button.transform);
        }
    }
}
