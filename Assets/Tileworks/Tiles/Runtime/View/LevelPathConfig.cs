using UnityEngine;

namespace Tileworks.Tiles.View
{
    // How the Home's level path winds, and how its nodes come in and idle.
    [CreateAssetMenu(menuName = "Tileworks/Tiles/Level Path")]
    public sealed class LevelPathConfig : ScriptableObject
    {
        [Header("Curve")]
        [SerializeField, Min(0f), Tooltip("Canvas units the nodes rise and fall either side of the middle line.")] private float _wave = 80f;
        [SerializeField, Range(0f, 180f), Tooltip("Degrees of the wave between neighbouring nodes.")] private float _waveStep = 90f;
        [SerializeField, Min(50f), Tooltip("Canvas units between neighbouring nodes; packed tighter when the width can't hold them all.")] private float _step = 185f;
        [SerializeField, Range(0f, 0.3f), Tooltip("Share of the width kept clear at each end.")] private float _endMargin = 0.05f;

        [Header("Nodes")]
        [SerializeField, Range(0.3f, 1.5f), Tooltip("Scale of a node against a level list tile.")] private float _nodeScale = 0.8f;
        [SerializeField, Range(1f, 2f), Tooltip("Scale of the player's current node.")] private float _currentScale = 1.3f;
        [SerializeField, Range(0.6f, 1f), Tooltip("Scale of the locked nodes ahead.")] private float _lockedScale = 0.85f;
        [SerializeField, Range(0, 8), Tooltip("Dots drawn between two neighbouring nodes.")] private int _dotsPerGap = 3;
        [SerializeField, Min(0f)] private float _dotSize = 24f;
        [SerializeField] private Color _clearedDot = new Color32(255, 236, 140, 255);
        [SerializeField] private Color _lockedDot = new Color(1f, 1f, 1f, 0.8f);
        [SerializeField, Tooltip("Where the mascot's feet stand, from the current node's centre; it peeks out from behind the node.")] private Vector2 _mascotOffset = new Vector2(0f, 80f);

        [Header("Motion")]
        [SerializeField, Range(1f, 1.3f), Tooltip("Peak of the current node's breathing, on top of its scale.")] private float _breatheScale = 1.06f;
        [SerializeField, Range(0.2f, 5f)] private float _breathePeriod = 1.8f;

        public float Wave => _wave;
        public float WaveStep => _waveStep;
        public float Step => _step;
        public float EndMargin => _endMargin;
        public float NodeScale => _nodeScale;
        public float CurrentScale => _currentScale;
        public float LockedScale => _lockedScale;
        public int DotsPerGap => _dotsPerGap;
        public float DotSize => _dotSize;
        public Color ClearedDot => _clearedDot;
        public Color LockedDot => _lockedDot;
        public Vector2 MascotOffset => _mascotOffset;
        public float BreatheScale => _breatheScale;
        public float BreathePeriod => _breathePeriod;
    }
}
