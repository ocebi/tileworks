using UnityEngine;

namespace BlockPuzzle.View
{
    // Board layout and animation timing; tuned here, not on the scene objects.
    [CreateAssetMenu(menuName = "Tileworks/Block Puzzle/Feel")]
    public sealed class PuzzleFeelConfig : ScriptableObject
    {
        [Header("Layout")]
        [SerializeField, Range(0f, 0.4f), Tooltip("Screen fraction kept free for the HUD.")] private float _topReserved = 0.16f;
        [SerializeField, Range(0f, 0.4f)] private float _bottomReserved = 0.03f;
        [SerializeField, Range(0.3f, 1f), Tooltip("Size of offered pieces relative to the board.")] private float _offerScale = 0.55f;
        [SerializeField, Min(0f), Tooltip("Board cells between the board and the offer.")] private float _offerGap = 0.6f;
        [SerializeField, Min(1f)] private float _offerHeight = 3.4f;
        [SerializeField, Min(0f), Tooltip("How far above the finger a lifted piece floats, in board cells.")] private float _lift = 1.6f;

        [Header("Timing (seconds)")]
        [SerializeField, Min(0f), Tooltip("Delay between neighbouring blocks of a cleared line.")] private float _clearStagger = 0.03f;
        [SerializeField, Min(0f)] private float _clearDuration = 0.22f;
        [SerializeField, Min(0f)] private float _dealDuration = 0.25f;
        [SerializeField, Min(0f), Tooltip("Pause between the last move and the result popup.")] private float _resultDelay = 0.6f;

        public float TopReserved => _topReserved;
        public float BottomReserved => _bottomReserved;
        public float OfferScale => _offerScale;
        public float OfferGap => _offerGap;
        public float OfferHeight => _offerHeight;
        public float Lift => _lift;
        public float ClearStagger => _clearStagger;
        public float ClearDuration => _clearDuration;
        public float DealDuration => _dealDuration;
        public float ResultDelay => _resultDelay;
    }
}
