using UnityEngine;

namespace Tileworks.Tiles.View
{
    // The booster bar's timings, shared by every game that has one.
    [CreateAssetMenu(menuName = "Tileworks/Booster Bar")]
    public sealed class BoosterBarConfig : ScriptableObject
    {
        [Header("Help bubble")]
        [SerializeField, Min(0.1f), Tooltip("How long a press must last before the bubble opens.")] private float _holdSeconds = 0.35f;
        [SerializeField, Min(0.1f), Tooltip("How long the bubble stays after a plain tap on a locked or empty booster.")] private float _bubbleSeconds = 1.8f;

        [Header("Picked booster")]
        [SerializeField] private Color _dim = new Color32(6, 16, 44, 158);
        [SerializeField, Min(0f)] private float _dimFade = 0.2f;
        [SerializeField, Min(0f), Tooltip("Canvas units the aim card drops in from.")] private float _cardDrop = 66f;
        [SerializeField, Min(0f)] private float _cardIn = 0.25f;
        [SerializeField, Min(0f)] private float _cardOut = 0.15f;
        [SerializeField, Min(0f), Tooltip("Canvas units the picked button rises.")] private float _pickLift = 28f;
        [SerializeField, Range(1f, 1.3f)] private float _pickScale = 1.08f;
        [SerializeField, Min(0f)] private float _pickDuration = 0.18f;

        public float HoldSeconds => _holdSeconds;
        public float BubbleSeconds => _bubbleSeconds;
        public Color Dim => _dim;
        public float DimFade => _dimFade;
        public float CardDrop => _cardDrop;
        public float CardIn => _cardIn;
        public float CardOut => _cardOut;
        public float PickLift => _pickLift;
        public float PickScale => _pickScale;
        public float PickDuration => _pickDuration;
    }
}
