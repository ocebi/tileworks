using System;
using UnityEngine;

namespace Tileworks.Tiles.View
{
    // One kind of board particle. Distances are in board cells, times in seconds.
    [Serializable]
    public struct ParticleLook
    {
        [SerializeField] private Sprite _sprite;
        [SerializeField, Range(0, 100), Tooltip("Sorting order; pieces sit at 10, flashes at 50.")] private int _order;
        [SerializeField] private Vector2 _speed;
        [SerializeField, Tooltip("Extra upward speed added to every particle.")] private float _lift;
        [SerializeField] private Vector2 _size;
        [SerializeField] private Vector2 _life;
        [SerializeField, Tooltip("Cells per second², down; negative rises.")] private float _gravity;
        [SerializeField, Range(0f, 1440f), Tooltip("Degrees per second either way.")] private float _spin;
        [SerializeField, Min(0f), Tooltip("Size at the end of its life, as a share of its start.")] private float _endSize;
        [SerializeField, Range(0f, 1f), Tooltip("Share of its life spent fading out.")] private float _fade;

        public ParticleLook(int order, Vector2 speed, float lift, Vector2 size, Vector2 life, float gravity, float spin, float endSize, float fade)
        {
            _sprite = null;
            _order = order;
            _speed = speed;
            _lift = lift;
            _size = size;
            _life = life;
            _gravity = gravity;
            _spin = spin;
            _endSize = endSize;
            _fade = fade;
        }

        public Sprite Sprite => _sprite;
        public int Order => _order;
        public Vector2 Speed => _speed;
        public float Lift => _lift;
        public Vector2 Size => _size;
        public Vector2 Life => _life;
        public float Gravity => _gravity;
        public float Spin => _spin;
        public float EndSize => _endSize;
        public float Fade => _fade;

        public ParticleLook WithSprite(Sprite sprite)
        {
            var look = this;
            look._sprite = sprite;
            return look;
        }
    }

    // Board particles, the board shake and the zoom punch for one game.
    [CreateAssetMenu(menuName = "Tileworks/Board Fx")]
    public sealed class BoardFxConfig : ScriptableObject
    {
        [Header("Looks")]
        [SerializeField] private ParticleLook _shard = new ParticleLook(55, new Vector2(1.8f, 3.6f), 1.5f, new Vector2(0.14f, 0.26f), new Vector2(0.4f, 0.6f), 18f, 540f, 0.6f, 0.4f);
        [SerializeField] private ParticleLook _smoke = new ParticleLook(52, new Vector2(0.3f, 1f), 0.4f, new Vector2(0.5f, 0.9f), new Vector2(0.6f, 0.9f), -0.6f, 60f, 1.8f, 0.7f);
        [SerializeField] private ParticleLook _spark = new ParticleLook(58, new Vector2(2.5f, 5f), 0.5f, new Vector2(0.08f, 0.14f), new Vector2(0.2f, 0.38f), 4f, 0f, 0.3f, 0.5f);
        [SerializeField] private ParticleLook _star = new ParticleLook(58, new Vector2(0.2f, 0.9f), 0.3f, new Vector2(0.15f, 0.3f), new Vector2(0.4f, 0.65f), 0f, 360f, 0.2f, 0.5f);
        [SerializeField, Tooltip("Shader for every look; it must take the sprite's texture and vertex colour.")] private Material _material;

        [Header("Amounts")]
        [SerializeField] private Vector2Int _shardsPerPiece = new Vector2Int(6, 10);
        [SerializeField] private Vector2Int _smokePerPuff = new Vector2Int(4, 6);
        [SerializeField] private Vector2Int _sparksPerBurst = new Vector2Int(8, 12);
        [SerializeField, Min(0f), Tooltip("Debris shards per cell of bomb radius.")] private float _debrisPerCell = 8f;
        [SerializeField, Min(0f), Tooltip("Rocket trail: smoke puffs and sparks per cell travelled.")] private Vector2 _trailPerCell = new Vector2(3f, 3f);
        [SerializeField, Range(0f, 1f), Tooltip("Share of every amount kept with Reduce Motion; trails stop.")] private float _reducedShare = 0.35f;

        [Header("Disco beams")]
        [SerializeField, Tooltip("Stretched along its length from the disco ball to a target.")] private Sprite _beam;
        [SerializeField, Range(0.05f, 1.5f), Tooltip("Beam thickness in cells.")] private float _beamWidth = 0.45f;
        [SerializeField, Min(0f), Tooltip("Seconds for a beam to grow out to its target.")] private float _beamGrow = 0.12f;
        [SerializeField, Min(0f), Tooltip("Seconds for a beam to fade once its target pops.")] private float _beamFade = 0.2f;

        [Header("Idle specials")]
        [SerializeField, Range(0f, 30f), Tooltip("Degrees either way a waiting rocket wiggles.")] private float _wiggleDegrees = 8f;
        [SerializeField, Min(0.5f), Tooltip("Seconds between one wiggle and the next.")] private float _wiggleEvery = 2.4f;
        [SerializeField, Min(0.05f), Tooltip("Seconds a wiggle lasts, dying away.")] private float _wiggleFor = 0.45f;
        [SerializeField, Min(1f), Tooltip("Swings per second during a wiggle.")] private float _wiggleRate = 7f;
        [SerializeField, Tooltip("Where a bomb's fuse is, from its centre, in cells.")] private Vector2 _fuseOffset = new Vector2(0.27f, 0.36f);
        [SerializeField, Min(0.02f), Tooltip("Seconds between a fuse's sparks.")] private float _fuseSparkEvery = 0.18f;
        [SerializeField, Min(0.02f), Tooltip("Seconds between a disco ball's twinkles.")] private float _twinkleEvery = 0.22f;
        [SerializeField, Range(0f, 1f), Tooltip("How far from a disco ball's centre it twinkles, in cells.")] private float _twinkleReach = 0.42f;

        [Header("Light sweep")]
        [SerializeField, Min(0f), Tooltip("Seconds for the band of light to cross the board on a combo or a big bomb.")] private float _sweepDuration = 0.4f;
        [SerializeField, Min(0.1f), Tooltip("Band width in cells.")] private float _sweepWidth = 1.6f;
        [SerializeField, Range(0f, 1f)] private float _sweepAlpha = 0.5f;
        [SerializeField, Range(-60f, 60f), Tooltip("Tilt of the band from vertical, in degrees.")] private float _sweepTilt = 25f;

        [Header("Shake and punch")]
        [SerializeField, Min(0f), Tooltip("Board shake per cell of bomb radius, in cells.")] private float _shakePerCell = 0.06f;
        [SerializeField, Min(0f)] private float _shakeDuration = 0.28f;
        [SerializeField, Min(1f)] private float _shakeFrequency = 24f;
        [SerializeField, Range(0f, 0.2f), Tooltip("Extra board scale at the peak of a zoom punch.")] private float _punchScale = 0.035f;
        [SerializeField, Min(0f)] private float _punchDuration = 0.3f;
        [SerializeField, Min(0.5f), Tooltip("Swings per second; about one swing over the duration reads as a single punch.")] private float _punchFrequency = 4f;

        public ParticleLook Shard => _shard;
        public ParticleLook Smoke => _smoke;
        public ParticleLook Spark => _spark;
        public ParticleLook Star => _star;
        public Material Material => _material;
        public Vector2Int ShardsPerPiece => _shardsPerPiece;
        public Vector2Int SmokePerPuff => _smokePerPuff;
        public Vector2Int SparksPerBurst => _sparksPerBurst;
        public float DebrisPerCell => _debrisPerCell;
        public Vector2 TrailPerCell => _trailPerCell;
        public Sprite BeamSprite => _beam;
        public float BeamWidth => _beamWidth;
        public float BeamGrow => _beamGrow;
        public float BeamFade => _beamFade;
        public float ReducedShare => _reducedShare;
        public float WiggleDegrees => _wiggleDegrees;
        public float WiggleEvery => _wiggleEvery;
        public float WiggleFor => _wiggleFor;
        public float WiggleRate => _wiggleRate;
        public Vector2 FuseOffset => _fuseOffset;
        public float FuseSparkEvery => _fuseSparkEvery;
        public float TwinkleEvery => _twinkleEvery;
        public float TwinkleReach => _twinkleReach;
        public float SweepDuration => _sweepDuration;
        public float SweepWidth => _sweepWidth;
        public float SweepAlpha => _sweepAlpha;
        public float SweepTilt => _sweepTilt;
        public float ShakePerCell => _shakePerCell;
        public float ShakeDuration => _shakeDuration;
        public float ShakeFrequency => _shakeFrequency;
        public float PunchScale => _punchScale;
        public float PunchDuration => _punchDuration;
        public float PunchFrequency => _punchFrequency;

        public void Configure(Material material, Sprite shard, Sprite smoke, Sprite spark, Sprite star, Sprite beam)
        {
            _material = material;
            _beam = beam;
            _shard = _shard.WithSprite(shard);
            _smoke = _smoke.WithSprite(smoke);
            _spark = _spark.WithSprite(spark);
            _star = _star.WithSprite(star);
        }
    }
}
