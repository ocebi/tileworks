using UnityEngine;

namespace Tileworks.Tiles.View
{
    // Timing of the level-start card, shared by every game that shows one.
    [CreateAssetMenu(menuName = "Tileworks/Level Intro")]
    public sealed class LevelIntroConfig : ScriptableObject
    {
        [SerializeField, Min(0f), Tooltip("Seconds the card stays before it closes by itself.")] private float _hold = 1.4f;
        [SerializeField, Min(0f), Tooltip("Longer for a Hard or Super Hard level, so the badge reads.")] private float _hardHold = 2f;
        [SerializeField, Min(0f), Tooltip("Delay before the badge drops onto the card.")] private float _badgeDelay = 0.25f;
        [SerializeField, Range(0f, 30f), Tooltip("Degrees the badge swings as it lands.")] private float _badgeSwing = 8f;
        [SerializeField, Min(0f), Tooltip("Seconds a goal icon takes to fly to its chip.")] private float _flight = 0.45f;
        [SerializeField, Min(0f), Tooltip("Gap between the goal icons taking off.")] private float _flightStagger = 0.07f;
        [SerializeField, Range(0f, 1f), Tooltip("Sideways bend of the flight, as a share of its length.")] private float _flightArc = 0.25f;

        public float Hold => _hold;
        public float HardHold => _hardHold;
        public float BadgeDelay => _badgeDelay;
        public float BadgeSwing => _badgeSwing;
        public float Flight => _flight;
        public float FlightStagger => _flightStagger;
        public float FlightArc => _flightArc;
    }
}
