using PrimeTween;
using UnityEngine;

namespace Hub.View
{
    // Hub timing and card emphasis; defaults follow the hub design.
    [CreateAssetMenu(menuName = "Tileworks/Hub/Feel")]
    public sealed class HubFeel : ScriptableObject
    {
        [Header("Backdrop (seconds)")]
        [SerializeField, Min(0f)] private float _crossfade = 0.35f;
        [SerializeField, Min(0f)] private float _reducedCrossfade = 0.15f;

        [Header("Cards")]
        [SerializeField, Min(1f)] private float _selectedScale = 1.08f;
        [SerializeField, Tooltip("Canvas units the selected card rises.")] private float _selectedLift = 20f;
        [SerializeField, Min(0f)] private float _selectDuration = 0.3f;
        [SerializeField] private Ease _selectEase = Ease.OutBack;
        [SerializeField, Range(0.5f, 1f)] private float _otherScale = 0.92f;
        [SerializeField, Min(0f)] private float _otherDuration = 0.15f;

        [Header("Glow")]
        [SerializeField, Range(0f, 1f)] private float _glowLow = 0.75f;
        [SerializeField, Min(0.1f), Tooltip("One fade down and back up.")] private float _glowPeriod = 0.7f;

        [Header("Leaving")]
        [SerializeField, Min(0f), Tooltip("Cards and Play fade out before the game loads.")] private float _leaveDuration = 0.2f;

        public float Crossfade(bool reduced) => reduced ? _reducedCrossfade : _crossfade;
        public float SelectedScale => _selectedScale;
        public float SelectedLift => _selectedLift;
        public float SelectDuration => _selectDuration;
        public Ease SelectEase => _selectEase;
        public float OtherScale => _otherScale;
        public float OtherDuration => _otherDuration;
        public float GlowLow => _glowLow;
        public float GlowPeriod => _glowPeriod;
        public float LeaveDuration => _leaveDuration;
    }
}
