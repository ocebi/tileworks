using PrimeTween;
using Tileworks.Kit.Haptics;
using Tileworks.Tiles.View;
using TileMatch.Model;

namespace TileMatch.View
{
    // What every board presenter needs, resolved once per level scope.
    public sealed class PresenterKit
    {
        private readonly IHaptics _haptics;

        public PresenterKit(BoardView board, TileMatchRules rules, PresentationClock clock, IHaptics haptics)
        {
            Board = board;
            Ids = rules.Events;
            Clock = clock;
            _haptics = haptics;
        }

        public BoardView Board { get; }
        public TrayView Tray => Board.Tray;
        public GameFeelConfig Feel => Board.Feel;
        public SfxPlayer Sfx => Board.Audio;
        public TileMatchEventIds Ids { get; }
        public PresentationClock Clock { get; }

        public float Duration(float seconds) => Clock.Scale(seconds);
        public float Now => Clock.Now;

        public void Feedback(Sfx cue, HapticKind haptic, float delay = 0f)
        {
            Sfx.Play(cue, delay);
            if (delay <= 0f) _haptics.Play(haptic);
            else Tween.Delay(_haptics, delay, h => h.Play(haptic), useUnscaledTime: true);
        }
    }
}
