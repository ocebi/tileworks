using System;
using Blast.Controller;
using Tileworks.Kit.App;
using Tileworks.Kit.UI;
using Tileworks.Kit.Unity;
using Tileworks.Tiles.View;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Blast.View
{
    public sealed class BlastHomeView : MonoBehaviour, IBlastHomeView
    {
        [SerializeField, ReadOnlyInInspector] private UiMotion _uiMotion;
        [SerializeField, ReadOnlyInInspector] private Button _play;
        [SerializeField, ReadOnlyInInspector] private TMP_Text _playLevel;
        [SerializeField, ReadOnlyInInspector] private LevelBadgeSticker _hard;
        [SerializeField, ReadOnlyInInspector, Tooltip("Parent of the Play button that breathes, so presses still scale the button.")]
        private RectTransform _playPulse;
        [SerializeField, ReadOnlyInInspector] private TMP_Text _coins;
        [SerializeField, ReadOnlyInInspector] private TMP_Text _lives;
        [SerializeField, ReadOnlyInInspector] private TMP_Text _livesTimer;
        [SerializeField, ReadOnlyInInspector] private Button _switchGame;

        private long _coinsShown = -1;
        private int _livesShown = -1;
        private bool? _playable;

        private IBlastHomeHandler _handler;
        private IGameSwitchHandler _switchHandler;

        private readonly TextNumbers _text = new TextNumbers();

        private void Awake()
        {
            _play.onClick.AddListener(() => _handler?.OnPlay());
            _switchGame.onClick.AddListener(() => _switchHandler?.OnSwitchGame());
        }

        public void SetLevel(int number) => _text.Set(_playLevel, "LEVEL ", number);

        public void Bind(IBlastHomeHandler handler) => _handler = handler;

        public void Bind(IGameSwitchHandler handler) => _switchHandler = handler;

        public void SetBadge(LevelBadge badge) => _hard.Show(badge, true);

        public void SetCoins(long coins)
        {
            if (_coinsShown < 0) _text.Clear().Append(coins, grouped: true).Apply(_coins);
            else if (coins != _coinsShown)
            {
                _uiMotion.Count(_coins, _coinsShown, coins);
                _uiMotion.Punch(_coins.transform);
            }

            _coinsShown = coins;
        }

        public void SetLives(int lives, in LivesTimer timer)
        {
            if (lives != _livesShown)
            {
                _text.Set(_lives, lives);
                if (_livesShown >= 0) _uiMotion.Punch(_lives.transform.parent);
                _livesShown = lives;
            }

            if (timer.Infinite) _livesTimer.text = "∞";
            else if (timer.Full) _livesTimer.text = "Full";
            else _text.SetClock(_livesTimer, timer.Seconds);
        }

        // Called every second with the lives timer, so the breathing loop only restarts when playability changes.
        public void SetPlayable(bool playable)
        {
            if (_playable == playable) return;
            _playable = playable;
            _play.interactable = playable;
            if (playable) _uiMotion.Breathe(_playPulse);
            else UiMotion.StopBreathing(_playPulse);
        }

        public void ShowSwitchGame(string label)
        {
            _switchGame.gameObject.SetActive(label != null);
            if (label != null) _switchGame.GetComponentInChildren<TMP_Text>().text = label;
        }
    }
}
