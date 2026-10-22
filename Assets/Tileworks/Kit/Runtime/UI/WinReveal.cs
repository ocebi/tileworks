using PrimeTween;
using Tileworks.Kit.Unity;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Tileworks.Kit.UI
{
    // The top of a win popup: confetti from both bottom corners, a glow turning behind a star with the level number that bursts
    // into little stars, then the score and the coin reward counting up with ticks, coins hopping out at the end. Games without a
    // score leave its row unset.
    public sealed class WinReveal : MonoBehaviour
    {
        [SerializeField, ReadOnlyInInspector] private UiMotion _uiMotion;
        [SerializeField, ReadOnlyInInspector] private Graphic _glow;
        [SerializeField, ReadOnlyInInspector] private Transform _hero;
        [SerializeField, ReadOnlyInInspector] private Transform _star;
        [SerializeField, ReadOnlyInInspector] private TMP_Text _level;
        [SerializeField, ReadOnlyInInspector] private Transform[] _sparkles = new Transform[0];
        [SerializeField, ReadOnlyInInspector] private Transform _scoreRow;
        [SerializeField, ReadOnlyInInspector] private TMP_Text _score;
        [SerializeField, ReadOnlyInInspector] private Transform _coinRow;
        [SerializeField, ReadOnlyInInspector] private TMP_Text _coins;
        [SerializeField, ReadOnlyInInspector] private Transform _coinIcon;
        [SerializeField, ReadOnlyInInspector] private UiBurst _confetti;
        [SerializeField, ReadOnlyInInspector] private UiBurst _starBurst;
        [SerializeField, ReadOnlyInInspector] private UiBurst _coinBurst;

        // Starts after `delay`; returns the delay for whatever pops in after it.
        public float Play(int level, int score, int coins, float delay)
        {
            gameObject.SetActive(true);
            var reduced = _uiMotion.Reduced;
            var config = _uiMotion.Config;
            var gap = reduced ? 0f : config.RevealGap;
            _level.SetText("{0}", level);
            Tween.StopAll(_glow);
            if (reduced) _glow.color = Color.white;
            else Tween.Alpha(_glow, 0f, 1f, config.RevealGap, startDelay: delay, useUnscaledTime: true);
            _uiMotion.Spin(_glow.transform);
            _uiMotion.Breathe(_hero);
            _uiMotion.PopIn(_star, delay);
            Tween.StopAll(this);
            Tween.Delay(this, delay, reveal => reveal.Celebrate(), useUnscaledTime: true);
            Tween.Delay(this, delay + config.PopDuration * 0.6f, reveal => reveal.StarLanded(), useUnscaledTime: true);
            for (var i = 0; i < _sparkles.Length; i++) _uiMotion.Twinkle(_sparkles[i], delay + gap + i * config.TwinklePeriod / _sparkles.Length);

            var at = delay + gap;
            if (_scoreRow != null)
            {
                _uiMotion.PopIn(_scoreRow, at);
                _uiMotion.Count(_score, 0f, score, delay: at + config.PopDuration, ticks: true);
                at += gap;
            }

            _coinRow.gameObject.SetActive(coins > 0);
            if (coins > 0)
            {
                _uiMotion.PopIn(_coinRow, at);
                _uiMotion.Count(_coins, 0f, coins, "+{0:0}", at + config.PopDuration, ticks: true);
                Tween.StopAll(_coinIcon);
                Tween.Delay(this, at + config.PopDuration + (reduced ? 0f : config.CountDuration), reveal => reveal.CoinsCounted(), useUnscaledTime: true);
                at += gap;
            }

            return at;
        }

        // Freezes the endless loops, for when the popup closes or shows something else.
        public void Stop()
        {
            Tween.StopAll(this);
            Tween.StopAll(_glow);
            Tween.StopAll(_glow.transform);
            UiMotion.StopBreathing(_hero);
            foreach (var sparkle in _sparkles) Tween.StopAll(sparkle);
        }

        private void Celebrate()
        {
            _confetti.transform.SetAsLastSibling();
            _confetti.Cannons();
            _uiMotion.Play(UiSound.Confetti);
        }

        private void StarLanded()
        {
            _starBurst.Burst(_star.position);
            _uiMotion.Play(UiSound.StarPop);
        }

        private void CoinsCounted()
        {
            _coinBurst.Burst(_coinIcon.position);
            _uiMotion.Punch(_coinIcon);
        }

        private void OnDestroy() => Tween.StopAll(this);

        public void Hide()
        {
            Stop();
            _confetti.Clear();
            _starBurst.Clear();
            _coinBurst.Clear();
            gameObject.SetActive(false);
        }
    }
}
