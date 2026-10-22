using Tileworks.Kit.Unity;
using UnityEngine;

namespace Tileworks.Tiles.View
{
    // Art and timing of the Hammer and Shuffle effects on Blast's and Gem Swap's boards. Sizes are in cells, times in
    // seconds, angles are z rotations in degrees.
    public sealed class BoosterFxConfig : ScriptableObject
    {
        [SerializeField, ReadOnlyInInspector, Tooltip("Pivot at the handle end.")] private Sprite _hammer;
        [SerializeField, ReadOnlyInInspector] private Sprite _shuffle;
        [SerializeField, ReadOnlyInInspector] private Sprite _aimRing;

        [Header("Aim")]
        [SerializeField] private Color _aimColour = new Color32(255, 216, 78, 255);
        [SerializeField, Min(0.1f)] private float _aimSize = 1.28f;
        [SerializeField, Range(0f, 30f)] private float _refuseDegrees = 6f;
        [SerializeField, Min(0f)] private float _refuseDuration = 0.15f;

        [Header("Hammer")]
        [SerializeField, Min(0.1f)] private float _hammerSize = 1.8f;
        [SerializeField, Min(0f), Tooltip("Handle end to the head's centre.")] private float _hammerReach = 1.07f;
        [SerializeField, Min(0f), Tooltip("Head's centre to its striking face.")] private float _hammerFace = 0.64f;
        [SerializeField, Tooltip("Where the face lands above the target's centre.")] private float _faceAboveCentre = 0.4f;
        [SerializeField, Tooltip("Handle end from the button at the start scale, in hammer widths, y up.")] private Vector2 _flyInOffset = new Vector2(-0.15f, -0.18f);
        [SerializeField, Tooltip("Arrive, wind up, strike, recoil, leave.")] private float[] _hammerAngles = { 20f, 38f, -50f, -36f, -20f };
        [SerializeField, Tooltip("Height of the handle end above rest at the same moments.")] private float[] _hammerLifts = { 0f, 0.12f, 0f, 0.16f, 0.44f };
        [SerializeField, Tooltip("Fly in, wind up, swing, recoil, leave.")] private float[] _hammerTimes = { 0.24f, 0.12f, 0.08f, 0.1f, 0.2f };
        [SerializeField, Min(0f), Tooltip("Scale leaving the button and leaving the board.")] private Vector2 _hammerScales = new Vector2(0.5f, 0.9f);

        [Header("Impact")]
        [SerializeField, Min(0f)] private float _flashSize = 1.8f;
        [SerializeField, Min(0f)] private float _ringSize = 2.2f;
        [SerializeField, Range(0, 16)] private int _sparks = 6;
        [SerializeField, Min(0f)] private float _sparkReach = 0.9f;
        [SerializeField, Min(0f)] private float _impactDuration = 0.3f;
        [SerializeField, Range(0.8f, 1f)] private float _boardPunch = 0.97f;
        [SerializeField, Tooltip("Board offsets in screen pixels, one per step.")] private float[] _boardShake = { 6f, -5f, 3f, 0f };
        [SerializeField, Min(0.01f)] private float _boardShakeStep = 0.04f;

        [Header("Shuffle")]
        [SerializeField, Min(0f), Tooltip("To the board's centre with one full turn.")] private float _shuffleFly = 0.3f;
        [SerializeField, Tooltip("Cells wide leaving the button and arriving.")] private Vector2 _shuffleScales = new Vector2(0.5f, 1.5f);
        [SerializeField, Min(0f)] private float _shuffleSwell = 2.2f;
        [SerializeField, Min(0f)] private float _shuffleFade = 0.2f;
        [SerializeField, Min(0f)] private float _shuffleRing = 4.4f;

        public Sprite Hammer => _hammer;
        public Sprite Shuffle => _shuffle;
        public Sprite AimRing => _aimRing;
        public Color AimColour => _aimColour;
        public float AimSize => _aimSize;
        public float RefuseDegrees => _refuseDegrees;
        public float RefuseDuration => _refuseDuration;
        public float HammerSize => _hammerSize;
        public float HammerReach => _hammerReach;
        public float HammerFace => _hammerFace;
        public float FaceAboveCentre => _faceAboveCentre;
        public Vector2 FlyInOffset => _flyInOffset;
        public float[] HammerAngles => _hammerAngles;
        public float[] HammerLifts => _hammerLifts;
        public float[] HammerTimes => _hammerTimes;
        public Vector2 HammerScales => _hammerScales;
        public float FlashSize => _flashSize;
        public float RingSize => _ringSize;
        public int Sparks => _sparks;
        public float SparkReach => _sparkReach;
        public float ImpactDuration => _impactDuration;
        public float BoardPunch => _boardPunch;
        public float[] BoardShake => _boardShake;
        public float BoardShakeStep => _boardShakeStep;
        public float ShuffleFly => _shuffleFly;
        public Vector2 ShuffleScales => _shuffleScales;
        public float ShuffleSwell => _shuffleSwell;
        public float ShuffleFade => _shuffleFade;
        public float ShuffleRing => _shuffleRing;

        public void Configure(Sprite hammer, Sprite shuffle, Sprite aimRing)
        {
            _hammer = hammer;
            _shuffle = shuffle;
            _aimRing = aimRing;
        }
    }
}
