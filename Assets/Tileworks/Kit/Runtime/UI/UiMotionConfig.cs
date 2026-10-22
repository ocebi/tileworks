using PrimeTween;
using UnityEngine;

namespace Tileworks.Kit.UI
{
    // Timing and easing of every UI animation, shared by all screens and popups.
    [CreateAssetMenu(menuName = "Tileworks/UI Motion")]
    public sealed class UiMotionConfig : ScriptableObject
    {
        [Header("Popups")]
        [SerializeField, Range(0.3f, 1f), Tooltip("Panel scale when it starts to open.")] private float _openScale = 0.7f;
        [SerializeField, Range(0f, 1f)] private float _openDuration = 0.35f;
        [SerializeField] private Ease _openEase = Ease.OutBack;
        [SerializeField, Range(0f, 1f), Tooltip("Fade of the dimmed background.")] private float _scrimDuration = 0.2f;
        [SerializeField, Range(0.5f, 1f), Tooltip("Panel scale when it finishes closing.")] private float _closeScale = 0.85f;
        [SerializeField, Range(0f, 1f)] private float _closeDuration = 0.18f;
        [SerializeField] private Ease _closeEase = Ease.InQuad;

        [Header("Buttons")]
        [SerializeField, Range(0.7f, 1f)] private float _pressScale = 0.92f;
        [SerializeField, Range(0f, 0.5f)] private float _pressDuration = 0.08f;
        [SerializeField, Range(0f, 1f)] private float _releaseDuration = 0.22f;
        [SerializeField] private Ease _releaseEase = Ease.OutBack;

        [Header("Feedback")]
        [SerializeField, Range(0f, 1f), Tooltip("Extra scale at the peak of a punch.")] private float _punchStrength = 0.2f;
        [SerializeField, Range(0f, 1f)] private float _punchDuration = 0.25f;
        [SerializeField, Range(0f, 45f), Tooltip("Degrees either way when an icon shakes.")] private float _shakeAngle = 10f;
        [SerializeField, Range(0f, 1f)] private float _shakeDuration = 0.3f;
        [SerializeField, Range(0f, 2f), Tooltip("Time for a number to count to its new value.")] private float _countDuration = 0.7f;
        [SerializeField] private Ease _countEase = Ease.OutCubic;
        [SerializeField, Range(0f, 1f), Tooltip("Time for a progress bar to fill to its new value.")] private float _fillDuration = 0.35f;
        [SerializeField] private Ease _fillEase = Ease.OutCubic;

        [Header("Reveal")]
        [SerializeField, Range(0f, 1f), Tooltip("Time for an item to pop in from nothing.")] private float _popDuration = 0.3f;
        [SerializeField] private Ease _popEase = Ease.OutBack;
        [SerializeField, Range(0f, 0.3f), Tooltip("Delay between items that pop in one after another.")] private float _stagger = 0.06f;

        [Header("Idle")]
        [SerializeField, Range(1f, 1.2f), Tooltip("Peak scale of the call-to-action breathing loop.")] private float _breatheScale = 1.04f;
        [SerializeField, Range(0.2f, 5f), Tooltip("Seconds for one grow-and-shrink.")] private float _breathePeriod = 1.6f;

        [Header("Win reveal")]
        [SerializeField, Range(0f, 1f), Tooltip("Delay between the star, the score and the coins.")] private float _revealGap = 0.25f;
        [SerializeField, Range(2f, 30f), Tooltip("Seconds for the glow behind the star to turn once.")] private float _spinPeriod = 14f;
        [SerializeField, Range(0.2f, 3f), Tooltip("Seconds for a sparkle to grow and fade.")] private float _twinklePeriod = 1.2f;

        [Header("Sound")]
        [SerializeField, Tooltip("Played when a button is clicked.")] private AudioClip _click;
        [SerializeField, Range(0f, 1f), Tooltip("Kept below the gameplay sounds.")] private float _clickVolume = 0.5f;
        [SerializeField] private UiSoundClip[] _sounds = new UiSoundClip[0];
        [SerializeField, Range(0f, 0.2f), Tooltip("A sound doesn't play again sooner than this.")] private float _soundGap = 0.04f;
        [SerializeField, Range(0.02f, 0.3f), Tooltip("Shortest time between two ticks while a number counts.")] private float _tickEvery = 0.07f;
        [SerializeField, Range(0, 24), Tooltip("Highest semitone a stepped sound climbs to.")] private int _stepCap = 12;

        [Header("Callouts and banners")]
        [SerializeField, Min(0f)] private float _calloutIn = 0.25f;
        [SerializeField, Min(0f)] private float _calloutHold = 0.5f;
        [SerializeField, Tooltip("Canvas units a callout rises while fading.")] private float _calloutRise = 30f;
        [SerializeField, Min(0f)] private float _calloutOut = 0.3f;
        [SerializeField, Min(0f)] private float _bannerIn = 0.3f;
        [SerializeField, Min(0f)] private float _bannerHold = 1f;
        [SerializeField, Min(0f)] private float _bannerOut = 0.25f;
        [SerializeField, Range(0.1f, 2f), Tooltip("A stepped run of sounds starts again from the bottom after this long without a step.")]
        private float _stepReset = 0.6f;

        [Header("Low moves")]
        [SerializeField, Min(0), Tooltip("The moves counter warns at this many moves or fewer.")] private int _lowMoves = 5;
        [SerializeField] private Color _alarmColour = new Color32(255, 72, 60, 255);
        [SerializeField, Range(1f, 1.4f)] private float _alarmScale = 1.14f;
        [SerializeField, Range(0.2f, 2f), Tooltip("Seconds for one pulse.")] private float _alarmPeriod = 0.7f;

        [Header("Menus")]
        [SerializeField, Range(0f, 10f), Tooltip("Seconds between sparkles drifting up a menu screen; 0 turns them off.")] private float _sparkleEvery = 2.5f;

        [Header("Less motion")]
        [SerializeField, Range(0f, 0.5f), Tooltip("Fade used instead of scaling, punching and shaking.")] private float _calmFade = 0.15f;

        public float OpenScale => _openScale;
        public float OpenDuration => _openDuration;
        public Ease OpenEase => _openEase;
        public float ScrimDuration => _scrimDuration;
        public float CloseScale => _closeScale;
        public float CloseDuration => _closeDuration;
        public Ease CloseEase => _closeEase;
        public float PressScale => _pressScale;
        public float PressDuration => _pressDuration;
        public float ReleaseDuration => _releaseDuration;
        public Ease ReleaseEase => _releaseEase;
        public float PunchStrength => _punchStrength;
        public float PunchDuration => _punchDuration;
        public float ShakeAngle => _shakeAngle;
        public float ShakeDuration => _shakeDuration;
        public float CountDuration => _countDuration;
        public Ease CountEase => _countEase;
        public float FillDuration => _fillDuration;
        public Ease FillEase => _fillEase;
        public float PopDuration => _popDuration;
        public Ease PopEase => _popEase;
        public float Stagger => _stagger;
        public float BreatheScale => _breatheScale;
        public float BreathePeriod => _breathePeriod;
        public float RevealGap => _revealGap;
        public float SpinPeriod => _spinPeriod;
        public float TwinklePeriod => _twinklePeriod;
        public float CalmFade => _calmFade;
        public AudioClip Click => _click;
        public float ClickVolume => _clickVolume;
        public float SoundGap => _soundGap;
        public float TickEvery => _tickEvery;
        public int StepCap => _stepCap;
        public float CalloutIn => _calloutIn;
        public float CalloutHold => _calloutHold;
        public float CalloutRise => _calloutRise;
        public float CalloutOut => _calloutOut;
        public float BannerIn => _bannerIn;
        public float BannerHold => _bannerHold;
        public float BannerOut => _bannerOut;
        public float StepReset => _stepReset;
        public int LowMoves => _lowMoves;
        public Color AlarmColour => _alarmColour;
        public float AlarmScale => _alarmScale;
        public float AlarmPeriod => _alarmPeriod;
        public float SparkleEvery => _sparkleEvery;

        public bool TryGetSound(UiSound sound, out UiSoundClip clip)
        {
            foreach (var entry in _sounds)
            {
                if (entry.Sound != sound || entry.Clip == null) continue;
                clip = entry;
                return true;
            }

            clip = default;
            return false;
        }

        public void SetSounds(UiSoundClip[] sounds) => _sounds = sounds;
    }
}
