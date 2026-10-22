using Tileworks.Tiles.View;
using UnityEngine;

namespace Blast.View
{
    // Board layout and animation timing; tuned here, not on the scene objects. Defaults follow the design's motion board.
    [CreateAssetMenu(menuName = "Tileworks/Blast/Feel")]
    public sealed class BlastFeelConfig : ScriptableObject
    {
        [Header("Layout")]
        [SerializeField, Range(0f, 0.4f), Tooltip("Screen fraction kept free for the HUD.")] private float _topReserved = 0.2f;
        [SerializeField, Range(0f, 0.4f)] private float _bottomReserved = 0.17f;

        [Header("Clear (seconds)")]
        [SerializeField, Min(1f)] private float _clearSquash = 1.18f;
        [SerializeField, Min(0f)] private float _clearDuration = 0.22f;
        [SerializeField, Min(0f), Tooltip("Delay per cell of distance from the tapped cube.")] private float _clearStagger = 0.035f;
        [SerializeField, Min(0f), Tooltip("The squash before a cleared piece shrinks away.")] private float _clearSquashTime = 0.06f;

        [Header("Special created")]
        [SerializeField, Min(0f), Tooltip("The group slides onto the tapped cell.")] private float _mergeDuration = 0.2f;
        [SerializeField, Min(0f)] private float _mergeStagger = 0.04f;
        [SerializeField, Min(0f)] private float _specialAppear = 0.28f;

        [Header("Fall")]
        [SerializeField, Min(0.01f), Tooltip("Cells per second a fall starts at.")] private float _fallStartSpeed = 4f;
        [SerializeField, Min(0f), Tooltip("Cells per second gained each second.")] private float _fallAcceleration = 60f;
        [SerializeField, Min(0.01f), Tooltip("Top speed in cells per second.")] private float _fallMaxSpeed = 24f;
        [SerializeField, Min(0.01f), Tooltip("A diagonal slide runs at its entry speed, kept between these.")] private float _slideMinSpeed = 8f;
        [SerializeField, Min(0.01f)] private float _slideMaxSpeed = 12f;
        [SerializeField, Range(0f, 1f), Tooltip("Share of the full squash a landing from rest gets.")] private float _landSquashFloor = 0.35f;
        [SerializeField, Range(0f, 0.2f), Tooltip("A clear made by a landing pops this long after touch-down.")] private float _landClearGap = 0.06f;
        [SerializeField, Min(0f), Tooltip("Delay per column away from where the move started.")] private float _fallColumnStagger = 0.03f;
        [SerializeField, Range(0f, 0.3f), Tooltip("Seconds a tap on a falling piece waits for it to land before it is dropped.")]
        private float _tapBuffer = 0.15f;
        [SerializeField] private Vector2 _landSquash = new Vector2(1.14f, 0.8f);
        [SerializeField, Min(0f)] private float _landDuration = 0.18f;

        [Header("Rockets")]
        [SerializeField, Min(0f), Tooltip("The two halves fly to the ends of the line.")] private float _rocketFlight = 0.42f;
        [SerializeField, Min(0f)] private float _rocketPopBase = 0.08f;
        [SerializeField, Min(0f), Tooltip("Pop delay per cell of distance.")] private float _rocketPopStep = 0.045f;

        [Header("Bombs")]
        [SerializeField, Range(0f, 45f)] private float _bombWobbleDegrees = 14f;
        [SerializeField, Min(0f)] private float _bombWobble = 0.14f;
        [SerializeField] private Vector2 _ringScale = new Vector2(0.2f, 2.6f);
        [SerializeField, Min(0f)] private float _ringDuration = 0.38f;
        [SerializeField, Min(0f)] private float _bombPopBase = 0.14f;
        [SerializeField, Min(0f), Tooltip("Pop delay per ring of distance.")] private float _bombPopStep = 0.055f;

        [Header("Disco")]
        [SerializeField, Min(0f)] private float _discoRay = 0.18f;
        [SerializeField, Min(0f), Tooltip("Gap between rays, per cell of distance.")] private float _discoRayStep = 0.06f;
        [SerializeField, Min(0f), Tooltip("A target pops this long after its ray starts.")] private float _discoPopAfter = 0.2f;

        [Header("Combo")]
        [SerializeField, Min(0f), Tooltip("The touching specials are pulled onto the tapped one.")] private float _comboGather = 0.24f;
        [SerializeField, Min(0f), Tooltip("The combined special swells and shakes while light gathers into it.")] private float _comboCharge = 0.4f;
        [SerializeField, Min(1f)] private float _comboSwell = 1.45f;
        [SerializeField, Range(0f, 45f)] private float _comboShakeDegrees = 10f;
        [SerializeField, Range(0, 24)] private int _comboSparks = 10;
        [SerializeField, Min(0f), Tooltip("Cells from the centre the sparks gather from and burst to.")] private float _comboSparkReach = 1.8f;
        [SerializeField, Min(0f)] private float _comboBurst = 0.35f;
        [SerializeField, Range(0.1f, 1f), Tooltip("A special pulled onto the other shrinks to this as it arrives.")] private float _comboArrivalScale = 0.7f;
        [SerializeField, Min(0f), Tooltip("World size of a gathering spark.")] private float _comboSparkSize = 0.5f;

        [Header("Goal flight")]
        [SerializeField, Min(0f), Tooltip("A goal piece pops up before it flies to its goal.")] private float _goalLift = 0.1f;
        [SerializeField, Min(1f)] private float _goalLiftScale = 1.3f;
        [SerializeField, Min(0.05f)] private float _goalFlight = 0.5f;
        [SerializeField, Range(0f, 0.6f), Tooltip("How far the path bends, as a share of its length.")] private float _goalArc = 0.2f;

        [Header("Hint")]
        [SerializeField, Min(0f), Tooltip("Idle seconds on a settled board before the best group pulses.")] private float _hintDelay = 5f;
        [SerializeField, Min(1f)] private float _hintScale = 1.1f;
        [SerializeField, Min(0.05f), Tooltip("Seconds per pulse, out and back.")] private float _hintPulse = 0.9f;

        [Header("Shuffle booster")]
        [SerializeField, Min(0f)] private float _shuffleLift = 0.12f;
        [SerializeField, Min(0f)] private float _shuffleFlight = 0.45f;
        [SerializeField, Min(0f)] private float _shuffleStagger = 0.12f;
        [SerializeField, Range(0.1f, 1f), Tooltip("Each piece shrinks to this as it lifts.")] private float _shuffleLiftScale = 0.85f;

        [Header("Effects")]
        [SerializeField, Tooltip("Tint of the light, sparks, trails and rings a special gives off.")] private Color _specialGlow = new Color(1f, 0.95f, 0.75f, 0.9f);
        [SerializeField, Tooltip("Tint of the fuse sparks on a resting bomb.")] private Color _bombFuse = new Color(1f, 0.85f, 0.4f, 1f);
        [SerializeField, Tooltip("Tint of the shards when a box breaks.")] private Color _boxDebris = new Color(0.78f, 0.55f, 0.32f, 1f);
        [SerializeField, Tooltip("Tint of a bomb's debris.")] private Color _bombDebris = new Color(1f, 0.82f, 0.45f, 1f);
        [SerializeField, Range(0, 12), Tooltip("Highest pitch step, in semitones, for the pops along a special's path.")] private int _popSteps = 8;
        [SerializeField, Range(0f, 3f), Tooltip("Debris radius in cells for a bomb; a big bomb and a clear-all scale it like their rings.")]
        private float _bombRadius = 1.5f;

        [Header("Level start")]
        [SerializeField, Range(0.1f, 1f), Tooltip("Scale a piece comes in at, on the board's top edge.")] private float _dropInScale = 0.6f;
        [SerializeField, Min(0f), Tooltip("Delay per row, bottom row first.")] private float _dropInRowStagger = 0.045f;
        [SerializeField, Min(0f)] private float _dropInColumnStagger = 0.012f;

        [Header("Bonus")]
        [SerializeField, Range(0, 20), Tooltip("Most leftover moves that turn into rockets after a win.")] private int _bonusRockets = 10;
        [SerializeField, Min(0f), Tooltip("Gap between one bonus rocket and the next.")] private float _bonusStagger = 0.16f;
        [SerializeField, Min(0f), Tooltip("A cube shows as a rocket this long before it goes off.")] private float _bonusFuse = 0.25f;

        [Header("Other")]
        [SerializeField, Min(0f)] private float _refuseDuration = 0.2f;
        [SerializeField, Min(0f), Tooltip("Pause between the last move and the result popup.")] private float _resultDelay = 0.6f;
        [SerializeField, Min(0f), Tooltip("After a win, the popup waits this long past the last goal piece landing.")] private float _winAfterLanding = 0.3f;

        [Header("Result popup")]
        [SerializeField, Min(0f), Tooltip("Canvas units between the content and each button.")] private float _resultButtonGap = 40f;
        [SerializeField, Min(0f), Tooltip("Canvas units below the last button.")] private float _resultBottomPadding = 70f;
        [SerializeField, Range(0f, 1f), Tooltip("Opacity of a settings toggle that is off.")] private float _toggleOffAlpha = 0.5f;

        public float TopReserved => _topReserved;
        public float BottomReserved => _bottomReserved;
        public float ClearSquash => _clearSquash;
        public float ClearDuration => _clearDuration;
        public float ClearStagger => _clearStagger;
        public float MergeDuration => _mergeDuration;
        public float MergeStagger => _mergeStagger;
        public float SpecialAppear => _specialAppear;
        public float FallColumnStagger => _fallColumnStagger;
        public float TapBuffer => _tapBuffer;
        public float LandClearGap => _landClearGap;
        public Vector2 LandSquash => _landSquash;
        public float LandDuration => _landDuration;
        public float RocketFlight => _rocketFlight;
        public float BombWobbleDegrees => _bombWobbleDegrees;
        public float BombWobble => _bombWobble;
        public Vector2 RingScale => _ringScale;
        public float RingDuration => _ringDuration;
        public float DiscoRay => _discoRay;
        public float ComboGather => _comboGather;
        public float ComboCharge => _comboCharge;
        public float ComboSwell => _comboSwell;
        public float ComboShakeDegrees => _comboShakeDegrees;
        public int ComboSparks => _comboSparks;
        public float ComboSparkReach => _comboSparkReach;
        public float ComboBurst => _comboBurst;
        public float GoalLift => _goalLift;
        public float GoalLiftScale => _goalLiftScale;
        public float GoalFlight => _goalFlight;
        public float GoalArc => _goalArc;
        public float HintDelay => _hintDelay;
        public float HintScale => _hintScale;
        public float HintPulse => _hintPulse;
        public float ShuffleLift => _shuffleLift;
        public float ShuffleFlight => _shuffleFlight;
        public float ShuffleStagger => _shuffleStagger;
        public float RefuseDuration => _refuseDuration;
        public float ResultDelay => _resultDelay;
        public float ClearSquashTime => _clearSquashTime;
        public float ComboArrivalScale => _comboArrivalScale;
        public float ComboSparkSize => _comboSparkSize;
        public Color SpecialGlow => _specialGlow;
        public Color BombFuse => _bombFuse;
        public float WinAfterLanding => _winAfterLanding;
        public float ResultButtonGap => _resultButtonGap;
        public float ResultBottomPadding => _resultBottomPadding;
        public float ToggleOffAlpha => _toggleOffAlpha;
        public float ShuffleLiftScale => _shuffleLiftScale;
        public int BonusRockets => _bonusRockets;
        public float BonusStagger => _bonusStagger;
        public float BonusFuse => _bonusFuse;
        public DropInStyle DropIn => new DropInStyle(_dropInRowStagger, _dropInColumnStagger, _dropInScale, Motion, _landDuration, _landSquash);
        public Color BoxDebris => _boxDebris;
        public Color BombDebris => _bombDebris;
        public int PopSteps => _popSteps;
        public float BombRadius => _bombRadius;

        public FallMotion Motion => new FallMotion(_fallStartSpeed, _fallAcceleration, _fallMaxSpeed, _slideMinSpeed, _slideMaxSpeed, _landSquashFloor);
        public float FallTime(float rows) => Motion.FallTime(rows);
        public float RocketPop(int distance) => _rocketPopBase + _rocketPopStep * distance;
        public float BombPop(int distance) => _bombPopBase + _bombPopStep * distance;
        public float DiscoPop(int distance) => _discoRayStep * distance + _discoPopAfter;
    }
}
