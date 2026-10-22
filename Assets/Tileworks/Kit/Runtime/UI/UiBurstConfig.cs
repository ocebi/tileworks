using UnityEngine;

namespace Tileworks.Kit.UI
{
    // One look for a UiBurst: how many bits fly, how fast and how long, in canvas units and seconds.
    [CreateAssetMenu(menuName = "Tileworks/UI Burst")]
    public sealed class UiBurstConfig : ScriptableObject
    {
        [Header("Amount")]
        [SerializeField, Range(1, 200)] private int _count = 12;
        [SerializeField, Range(0f, 1f), Tooltip("Share of bits kept when the player wants less motion.")] private float _reducedShare = 0.35f;

        [Header("Launch")]
        [SerializeField, Range(0f, 360f), Tooltip("Spread around the launch direction; 360 sprays all round.")] private float _spread = 360f;
        [SerializeField] private Vector2 _speed = new Vector2(500f, 900f);
        [SerializeField, Min(0f), Tooltip("Canvas units the bits start from around the launch point.")] private float _jitter = 10f;

        [Header("Flight")]
        [SerializeField, Tooltip("Pull in canvas units per second squared; negative falls.")] private float _gravity = -1600f;
        [SerializeField, Range(0f, 10f), Tooltip("How quickly bits lose speed.")] private float _drag = 2.5f;
        [SerializeField, Min(0f), Tooltip("Side-to-side flutter, as for paper.")] private float _sway;
        [SerializeField] private Vector2 _life = new Vector2(0.45f, 0.7f);
        [SerializeField, Range(0f, 1f), Tooltip("Share of the life after which a bit fades out.")] private float _fadeFrom = 0.6f;

        [Header("Look")]
        [SerializeField] private Vector2 _size = new Vector2(28f, 48f);
        [SerializeField, Tooltip("Degrees per second, either way.")] private Vector2 _spin = new Vector2(90f, 360f);
        [SerializeField, Range(0f, 0.3f), Tooltip("Seconds a bit takes to grow in.")] private float _growIn = 0.06f;
        [SerializeField, Tooltip("Each bit takes one; none keeps the sprite's colour.")] private Color[] _palette = new Color[0];

        [Header("Trail")]
        [SerializeField, Range(0.01f, 0.5f), Tooltip("Seconds between sprays when the burst follows something as a trail.")] private float _trailEvery = 0.04f;

        public int Count => _count;
        public float ReducedShare => _reducedShare;
        public float Spread => _spread;
        public Vector2 Speed => _speed;
        public float Jitter => _jitter;
        public float Gravity => _gravity;
        public float Drag => _drag;
        public float Sway => _sway;
        public Vector2 Life => _life;
        public float FadeFrom => _fadeFrom;
        public Vector2 Size => _size;
        public Vector2 Spin => _spin;
        public float GrowIn => _growIn;
        public Color[] Palette => _palette;
        public float TrailEvery => _trailEvery;
    }
}
