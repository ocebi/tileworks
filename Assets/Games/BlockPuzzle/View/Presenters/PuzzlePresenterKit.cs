using BlockPuzzle.Controller;
using BlockPuzzle.Model;
using PrimeTween;
using Tileworks.Kit.Haptics;
using Tileworks.Tiles.View;

namespace BlockPuzzle.View
{
    // What every puzzle presenter needs, resolved once per attempt scope.
    public sealed class PuzzlePresenterKit
    {
        private readonly IHaptics _haptics;

        public PuzzlePresenterKit(PuzzleBoardView board, IPuzzleHudView hud, BlockRules rules, PresentationClock clock, IHaptics haptics)
        {
            Board = board;
            Hud = hud;
            Ids = rules.Events;
            Clock = clock;
            _haptics = haptics;
        }

        public PuzzleBoardView Board { get; }
        public IPuzzleHudView Hud { get; }
        public BlockEventIds Ids { get; }
        public PresentationClock Clock { get; }
        public PuzzleSfx Sfx => Board.Sfx;
        public PuzzleFeelConfig Feel => Board.Feel;

        public float Duration(float seconds) => Clock.Scale(seconds);

        public void Feedback(PuzzleCue cue, HapticKind haptic, float delay = 0f, float pitch = 1f)
        {
            Sfx.Play(cue, delay, pitch);
            if (delay <= 0f) _haptics.Play(haptic);
            else Tween.Delay(_haptics, delay, h => h.Play(haptic), useUnscaledTime: true);
        }

        public void At<T>(T target, float delay, System.Action<T> action) where T : class
        {
            if (delay <= 0f) action(target);
            else Tween.Delay(target, delay, action, useUnscaledTime: true);
        }
    }
}
