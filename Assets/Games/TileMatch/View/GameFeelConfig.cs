using PrimeTween;
using UnityEngine;
using UnityEngine.Serialization;

namespace TileMatch.View
{
    // Board layout and every duration, ease and amplitude of its presentation; tuned here, not on the scene objects.
    [CreateAssetMenu(menuName = "Tileworks/Tile Match/Game Feel")]
    public sealed class GameFeelConfig : ScriptableObject
    {
        [Header("Layout")]
        [SerializeField, Range(0f, 0.4f), Tooltip("Screen fraction kept free for the HUD.")] private float _topReserved = 0.12f;
        [SerializeField, Range(0f, 0.4f), Tooltip("Screen fraction kept free for the boosters.")] private float _bottomReserved = 0.14f;
        [SerializeField, Min(0f), Tooltip("Distance from the board's lowest tile to the tray.")] private float _trayGap = 1.1f;
        [SerializeField, Min(0.1f)] private float _traySlotSpacing = 0.86f;
        [SerializeField, Min(0f)] private float _trayPadding = 0.3f;
        [SerializeField, Min(0.1f)] private float _trayHeight = 1.15f;

        [Header("Flight to tray")]
        [SerializeField, FormerlySerializedAs("FlightDuration"), Min(0f)] private float _flightDuration = 0.3f;
        [SerializeField, FormerlySerializedAs("ArcHeight"), Min(0f)] private float _arcHeight = 1.1f;
        [SerializeField, FormerlySerializedAs("FlightEase")] private Ease _flightEase = Ease.OutCubic;
        [SerializeField, FormerlySerializedAs("TrayScale"), Min(0f)] private float _trayScale = 0.82f;
        [SerializeField, FormerlySerializedAs("LandSquash"), Min(0f)] private float _landSquash = 0.12f;

        [Header("Tray")]
        [SerializeField, FormerlySerializedAs("SlideDuration"), Min(0f)] private float _slideDuration = 0.14f;
        [SerializeField, FormerlySerializedAs("SlideEase")] private Ease _slideEase = Ease.OutQuad;
        [SerializeField, FormerlySerializedAs("ClearGather"), Min(0f)] private float _clearGather = 0.1f;
        [SerializeField, FormerlySerializedAs("ClearPop"), Min(0f)] private float _clearPop = 0.2f;

        [Header("Board")]
        [SerializeField, FormerlySerializedAs("UncoverDuration"), Min(0f)] private float _uncoverDuration = 0.16f;
        [SerializeField, FormerlySerializedAs("FlipDuration"), Min(0f)] private float _flipDuration = 0.22f;
        [SerializeField, FormerlySerializedAs("SpawnDuration"), Min(0f)] private float _spawnDuration = 0.25f;
        [SerializeField, FormerlySerializedAs("ReturnDuration"), Min(0f)] private float _returnDuration = 0.3f;
        [SerializeField, FormerlySerializedAs("NopeShake"), Min(0f)] private float _nopeShake = 0.08f;
        [SerializeField, FormerlySerializedAs("NopeDuration"), Min(0f)] private float _nopeDuration = 0.25f;
        [SerializeField, FormerlySerializedAs("ResultDelay"), Min(0f)] private float _resultDelay = 0.35f;

        public float TopReserved => _topReserved;
        public float BottomReserved => _bottomReserved;
        public float TrayGap => _trayGap;
        public float TraySlotSpacing => _traySlotSpacing;
        public float TrayPadding => _trayPadding;
        public float TrayHeight => _trayHeight;
        public float FlightDuration => _flightDuration;
        public float ArcHeight => _arcHeight;
        public Ease FlightEase => _flightEase;
        public float TrayScale => _trayScale;
        public float LandSquash => _landSquash;
        public float SlideDuration => _slideDuration;
        public Ease SlideEase => _slideEase;
        public float ClearGather => _clearGather;
        public float ClearPop => _clearPop;
        public float UncoverDuration => _uncoverDuration;
        public float FlipDuration => _flipDuration;
        public float SpawnDuration => _spawnDuration;
        public float ReturnDuration => _returnDuration;
        public float NopeShake => _nopeShake;
        public float NopeDuration => _nopeDuration;
        public float ResultDelay => _resultDelay;
    }
}
