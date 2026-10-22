using PrimeTween;
using Tileworks.Kit.Unity;
using TMPro;
using UnityEngine;

namespace Tileworks.Kit.UI
{
    // One per scene. Plays the shared UI animations and sounds, and turns the animations into short fades when the player wants
    // less motion.
    // UI tweens use unscaled time so they keep running while the game is paused.
    public sealed class UiMotion : MonoBehaviour
    {
        [SerializeField, ReadOnlyInInspector] private UiMotionConfig _config;

        private const int Voices = 4;

        private readonly float[] _lastPlayed = new float[System.Enum.GetValues(typeof(UiSound)).Length];
        private IMotionPreference _preference;
        private AudioSource[] _voices;
        private int _nextVoice;
        private float _lastTick = -1f;

        public UiMotionConfig Config => _config;
        public bool Reduced => _preference != null && _preference.ReduceMotion;

        public void Bind(IMotionPreference preference) => _preference = preference;

        public Sequence Open(CanvasGroup group, Transform panel)
        {
            Play(UiSound.PopupOpen);
            SetInteractive(group, true);
            panel.localScale = Vector3.one;
            if (Reduced) return Sequence.Create(useUnscaledTime: true).Group(Tween.Alpha(group, 0f, 1f, _config.CalmFade, Ease.Linear));
            return Sequence.Create(useUnscaledTime: true)
                .Group(Tween.Alpha(group, 0f, 1f, _config.ScrimDuration, Ease.OutQuad))
                .Group(Tween.Scale(panel, _config.OpenScale, 1f, _config.OpenDuration, _config.OpenEase));
        }

        public Sequence Close(CanvasGroup group, Transform panel)
        {
            Play(UiSound.PopupClose);
            SetInteractive(group, false);
            if (Reduced) return Sequence.Create(useUnscaledTime: true).Group(Tween.Alpha(group, group.alpha, 0f, _config.CalmFade, Ease.Linear));
            return Sequence.Create(useUnscaledTime: true)
                .Group(Tween.Alpha(group, group.alpha, 0f, _config.CloseDuration, _config.CloseEase))
                .Group(Tween.Scale(panel, panel.localScale.x, _config.CloseScale, _config.CloseDuration, _config.CloseEase));
        }

        public static void HideNow(CanvasGroup group)
        {
            group.alpha = 0f;
            SetInteractive(group, false);
        }

        // Button feedback stays on with less motion: it is small and confirms the touch.
        public void Press(Transform target)
        {
            Tween.StopAll(target);
            Tween.Scale(target, _config.PressScale, _config.PressDuration, Ease.OutQuad, useUnscaledTime: true);
        }

        public void Release(Transform target)
        {
            Tween.StopAll(target);
            Tween.Scale(target, 1f, _config.ReleaseDuration, _config.ReleaseEase, useUnscaledTime: true);
        }

        public void PlayClick()
        {
            if (_config.Click != null) Voice().PlayOneShot(_config.Click, _config.ClickVolume);
        }

        public void Play(UiSound sound, float pitch = 1f)
        {
            if (!_config.TryGetSound(sound, out var entry)) return;
            var last = _lastPlayed[(int)sound];
            if (last > 0f && Time.unscaledTime - last < _config.SoundGap) return;
            _lastPlayed[(int)sound] = Time.unscaledTime;
            var voice = Voice();
            voice.clip = entry.Clip;
            voice.volume = entry.Volume <= 0f ? 1f : entry.Volume;
            voice.pitch = pitch;
            voice.Play();
        }

        // Each step plays a semitone higher, up to the config's cap, for runs such as goal pieces landing one after another.
        public void PlayStep(UiSound sound, int step) => Play(sound, Mathf.Pow(2f, Mathf.Clamp(step, 0, _config.StepCap) / 12f));

        // A word that pops in tilted, holds, then rises and fades; a new one replaces the one showing. `home` is where it rests.
        public void Callout(TMP_Text label, string text, Vector2 home)
        {
            var rect = (RectTransform)label.transform;
            Tween.StopAll(rect);
            Tween.StopAll(label);
            label.text = text;
            label.alpha = 1f;
            label.gameObject.SetActive(true);
            rect.anchoredPosition = home;
            Play(UiSound.Callout);
            var hold = _config.CalloutIn + _config.CalloutHold;
            if (Reduced)
            {
                rect.localScale = Vector3.one;
                rect.localRotation = Quaternion.identity;
                Tween.Alpha(label, 0f, _config.CalmFade, startDelay: hold, useUnscaledTime: true).OnComplete(label, l => l.gameObject.SetActive(false));
                return;
            }

            rect.localScale = Vector3.one * 0.4f;
            rect.localRotation = Quaternion.Euler(0f, 0f, -6f);
            Tween.Scale(rect, 1f, _config.CalloutIn, Ease.OutBack, useUnscaledTime: true);
            Tween.LocalRotation(rect, Quaternion.identity, _config.CalloutIn, Ease.OutBack, useUnscaledTime: true);
            Tween.UIAnchoredPositionY(rect, home.y + _config.CalloutRise, _config.CalloutOut, Ease.OutQuad, startDelay: hold, useUnscaledTime: true);
            Tween.Alpha(label, 0f, _config.CalloutOut, Ease.InQuad, startDelay: hold, useUnscaledTime: true).OnComplete(label, l => l.gameObject.SetActive(false));
        }

        // A ribbon that pops up, holds and shrinks away. Returns the seconds until it is gone.
        public float Banner(RectTransform banner)
        {
            Tween.StopAll(banner);
            banner.gameObject.SetActive(true);
            var hold = _config.BannerIn + _config.BannerHold;
            if (Reduced)
            {
                banner.localScale = Vector3.one;
                Tween.Delay(banner, hold, b => b.gameObject.SetActive(false), useUnscaledTime: true);
                return hold;
            }

            banner.localScale = Vector3.zero;
            Tween.Scale(banner, 1f, _config.BannerIn, Ease.OutBack, useUnscaledTime: true);
            Tween.Scale(banner, 0f, _config.BannerOut, Ease.InBack, startDelay: hold, useUnscaledTime: true).OnComplete(banner, b => b.gameObject.SetActive(false));
            return hold + _config.BannerOut;
        }

        // A pulsing red number for a counter that is running out; `calm` is its normal colour. A plain colour change with less motion.
        public void Alarm(TMP_Text label, Color calm, bool on)
        {
            var target = label.transform;
            Tween.StopAll(target);
            target.localScale = Vector3.one;
            label.color = on ? _config.AlarmColour : calm;
            if (!on || Reduced) return;
            Tween.Scale(target, 1f, _config.AlarmScale, _config.AlarmPeriod * 0.5f, Ease.InOutSine, cycles: -1, cycleMode: CycleMode.Yoyo,
                useUnscaledTime: true);
        }

        private AudioSource Voice()
        {
            if (_voices == null)
            {
                _voices = new AudioSource[Voices];
                for (var i = 0; i < Voices; i++)
                {
                    _voices[i] = gameObject.AddComponent<AudioSource>();
                    _voices[i].playOnAwake = false;
                }
            }

            foreach (var voice in _voices)
                if (!voice.isPlaying)
                    return voice;

            var oldest = _voices[_nextVoice];
            _nextVoice = (_nextVoice + 1) % Voices;
            return oldest;
        }

        private void Tick()
        {
            if (Time.unscaledTime - _lastTick < _config.TickEvery) return;
            _lastTick = Time.unscaledTime;
            Play(UiSound.CountTick);
        }

        private void OnDestroy() => Tween.StopAll(this);

        // A quick grow and settle that draws the eye to a value that changed.
        public Tween Punch(Transform target)
        {
            Tween.StopAll(target);
            target.localScale = Vector3.one;
            if (Reduced) return default;
            return Tween.Scale(target, 1f, 1f + _config.PunchStrength, _config.PunchDuration * 0.5f, Ease.OutQuad, cycles: 2, cycleMode: CycleMode.Yoyo,
                useUnscaledTime: true);
        }

        public Tween Shake(Transform target)
        {
            Tween.StopAll(target);
            target.localRotation = Quaternion.identity;
            if (Reduced) return default;
            return Tween.ShakeLocalRotation(target, new Vector3(0f, 0f, _config.ShakeAngle), _config.ShakeDuration, frequency: 8f, useUnscaledTime: true);
        }

        // Scales an item up from nothing after a delay; callers stagger the delays to reveal a group.
        public Tween PopIn(Transform target, float delay)
        {
            Tween.StopAll(target);
            if (Reduced)
            {
                target.localScale = Vector3.one;
                return default;
            }

            target.localScale = Vector3.zero;
            return Tween.Scale(target, 0f, 1f, _config.PopDuration, _config.PopEase, startDelay: delay, useUnscaledTime: true);
        }

        public float StaggerDelay(int index) => Reduced ? 0f : _config.OpenDuration * 0.5f + index * _config.Stagger;

        // Counts a number label to a new value; TMP formats the digits without allocating. The format may wrap the number, as in "+{0:0}".
        // With `ticks`, a short tick sounds as it counts.
        public Tween Count(TMP_Text label, float from, float to, string format = "{0:0}", float delay = 0f, bool ticks = false)
        {
            Tween.StopAll(label);
            if (Reduced || from == to)
            {
                label.SetText(format, to);
                return default;
            }

            label.SetText(format, from);
            if (ticks) Tween.Custom(this, 0f, 1f, _config.CountDuration, (UiMotion motion, float _) => motion.Tick(), startDelay: delay, useUnscaledTime: true);
            return Tween.Custom(label, from, to, _config.CountDuration, (TMP_Text text, float value) => text.SetText(format, value), _config.CountEase,
                startDelay: delay, useUnscaledTime: true);
        }

        // Moves the right edge of a progress bar fill to a fraction of its parent.
        public Tween Fill(RectTransform fill, float to)
        {
            Tween.StopAll(fill);
            to = Mathf.Clamp01(to);
            if (Reduced)
            {
                fill.anchorMax = new Vector2(to, fill.anchorMax.y);
                return default;
            }

            return Tween.Custom(fill, fill.anchorMax.x, to, _config.FillDuration, (RectTransform rect, float x) => rect.anchorMax = new Vector2(x, rect.anchorMax.y),
                _config.FillEase, useUnscaledTime: true);
        }

        // An endless gentle grow-and-shrink for the main call to action; none with less motion.
        public Tween Breathe(Transform target)
        {
            Tween.StopAll(target);
            target.localScale = Vector3.one;
            if (Reduced) return default;
            return Tween.Scale(target, 1f, _config.BreatheScale, _config.BreathePeriod * 0.5f, Ease.InOutSine, cycles: -1, cycleMode: CycleMode.Yoyo,
                useUnscaledTime: true);
        }

        // An endless slow turn, like the glow behind a reward; none with less motion.
        public Tween Spin(Transform target)
        {
            Tween.StopAll(target);
            target.localRotation = Quaternion.identity;
            if (Reduced) return default;
            return Tween.LocalEulerAngles(target, Vector3.zero, new Vector3(0f, 0f, -360f), _config.SpinPeriod, Ease.Linear, cycles: -1, useUnscaledTime: true);
        }

        // An endless sparkle that grows from nothing and fades back, starting after a delay so a group twinkles out of step.
        public Tween Twinkle(Transform target, float delay)
        {
            Tween.StopAll(target);
            if (Reduced)
            {
                target.localScale = Vector3.one * 0.8f;
                return default;
            }

            target.localScale = Vector3.zero;
            var half = _config.TwinklePeriod * 0.5f;
            return Tween.Delay(target, delay, t => Tween.Scale(t, 0f, 1f, half, Ease.InOutSine, cycles: -1, cycleMode: CycleMode.Yoyo, useUnscaledTime: true),
                useUnscaledTime: true);
        }

        public static void StopBreathing(Transform target)
        {
            Tween.StopAll(target);
            target.localScale = Vector3.one;
        }

        private static void SetInteractive(CanvasGroup group, bool on)
        {
            group.interactable = on;
            group.blocksRaycasts = on;
        }
    }
}
