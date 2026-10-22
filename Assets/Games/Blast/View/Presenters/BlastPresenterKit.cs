using Blast.Controller;
using Blast.Model;
using PrimeTween;
using Tileworks.Kit.Haptics;
using Tileworks.Tiles.View;
using UnityEngine;

namespace Blast.View
{
    // What every blast presenter needs, resolved once per attempt scope.
    public sealed class BlastPresenterKit
    {
        private readonly IHaptics _haptics;

        public BlastPresenterKit(BlastBoardView board, IBlastHudView hud, IBoosterBarView boosterBar, BlastRules rules, PresentationClock clock,
            IHaptics haptics)
        {
            Board = board;
            BoosterBar = boosterBar;
            Hud = hud;
            Ids = rules.Events;
            Clock = clock;
            _haptics = haptics;
            board.BindClock(clock);
            if (board.Effects != null) board.Effects.Bind(clock);
        }

        public BlastBoardView Board { get; }
        public IBlastHudView Hud { get; }
        public IBoosterBarView BoosterBar { get; }
        public BlastEventIds Ids { get; }
        public PresentationClock Clock { get; }
        public BlastSfx Sfx => Board.Sfx;
        public BlastFeelConfig Feel => Board.Feel;

        // Set by the clear presenter for every piece; a goal presenter that follows it in the same batch flies that piece.
        public ClearedPiece LastCleared { get; set; }

        // Clock time the last goal piece lands; the win waits for it.
        public float LandsAt { get; set; }

        // From the second special in an action on, each one calls out a little louder.
        public void ShowChain(int chain, float delay)
        {
            if (chain >= 2) At(Hud, delay, hud => hud.ShowCascade(chain));
        }

        // Where the last disco ball went off; its rays start there.
        public Vector3 DiscoAt { get; set; }

        // Clock time the shuffle booster's lead-in ends; the pieces fly after it.
        public float HoldUntil { get; set; }

        // Holds the board for an effect that starts after `delay` and lasts `seconds`; returns `seconds`.
        public float Hold(float delay, float seconds)
        {
            Board.HoldEffects(Clock.Now + delay + seconds);
            return seconds;
        }

        // `delay` pushed back past the hold.
        public float AfterHold(float delay) => Mathf.Max(delay, HoldUntil - Clock.Now);

        public GoalFlight Flight => new GoalFlight(Duration(Feel.GoalLift), Feel.GoalLiftScale, Duration(Feel.GoalFlight), Feel.GoalArc);

        public float Duration(float seconds) => Clock.Scale(seconds);

        public void Feedback(BlastCue cue, HapticKind haptic, float delay = 0f, float pitch = 1f)
        {
            Sfx.Play(cue, delay, pitch);
            if (delay <= 0f) _haptics.Play(haptic);
            else Tween.Delay(_haptics, delay, h => h.Play(haptic), useUnscaledTime: true);
        }

        public void Shatter(Vector3 at, Color colour, float delay)
        {
            if (Board.Effects != null) Board.Effects.Shatter(at, colour, delay);
        }

        public void Sparks(Vector3 at, Color colour, float delay)
        {
            if (Board.Effects != null) Board.Effects.Sparks(at, colour, delay);
        }

        public void Explode(Vector3 at, float radius, Color colour, float delay)
        {
            if (Board.Effects != null) Board.Effects.Explode(at, radius, colour, delay);
        }

        // A beam from `from` that grows out to `to` at `delay`, holds for `hold`, then fades; stars burst where it lands.
        public void Beam(Vector3 from, Vector3 to, Color colour, float delay, float hold)
        {
            var effects = Board.Effects;
            if (effects == null || effects.Config.BeamSprite == null) return;
            var config = effects.Config;
            var offset = to - from;
            var length = offset.magnitude;
            if (length < 0.01f) return;
            var size = config.BeamSprite.bounds.size;
            var width = config.BeamWidth / size.y;
            var beam = Board.Fx(config.BeamSprite, from, new Vector2(0f, width), Color.clear);
            beam.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(offset.y, offset.x) * Mathf.Rad2Deg);
            var grow = Duration(config.BeamGrow);
            Fade(beam, colour, 0f, colour.a, grow, Ease.OutQuad, delay);
            Tween.ScaleX(beam.transform, 0f, length / size.x, grow, Ease.OutQuad, startDelay: delay);
            Tween.LocalPosition(beam.transform, from, from + offset * 0.5f, grow, Ease.OutQuad, startDelay: delay);
            Tween.Alpha(beam, colour.a, 0f, Duration(config.BeamFade), Ease.InQuad, startDelay: delay + Mathf.Max(grow, hold))
                .OnComplete(Board, b => b.Release(beam));
            effects.Stars(to, colour, delay + grow);
        }

        public void Trail(Transform target, Color colour, float delay, float seconds)
        {
            if (Board.Effects != null) Board.Effects.Follow(target, colour, delay, seconds);
        }

        public void Shake(float radius, float delay)
        {
            if (Board.Effects != null) Board.Effects.Shake(radius, delay);
        }

        public void Punch(float delay)
        {
            if (Board.Effects != null) Board.Effects.Punch(delay);
        }

        // A tilted band of light crossing the board from left to right, for the biggest moments.
        public void Sweep(float delay)
        {
            var effects = Board.Effects;
            if (effects == null || effects.Reduced || effects.Config.BeamSprite == null) return;
            var config = effects.Config;
            var size = config.BeamSprite.bounds.size;
            var span = Board.Width * 0.5f + config.SweepWidth;
            var length = Board.Height + 2f * config.SweepWidth;
            var band = Board.Fx(config.BeamSprite, new Vector3(-span, 0f, 0f), new Vector2(length / size.x, config.SweepWidth / size.y), Color.clear);
            band.transform.localRotation = Quaternion.Euler(0f, 0f, 90f + config.SweepTilt);
            var duration = Duration(config.SweepDuration);
            var light = new Color(1f, 1f, 1f, config.SweepAlpha);
            Fade(band, light, 0f, light.a, duration * 0.25f, Ease.OutQuad, delay);
            Tween.Alpha(band, light.a, 0f, duration * 0.3f, Ease.InQuad, startDelay: delay + duration * 0.7f);
            Tween.LocalPosition(band.transform, new Vector3(-span, 0f, 0f), new Vector3(span, 0f, 0f), duration, Ease.InOutQuad, startDelay: delay)
                .OnComplete(Board, b => b.Release(band));
        }

        // Fades by alpha only, so a tinted sprite never passes through black on the way in or out. Hidden until `delay`.
        public Tween Fade(SpriteRenderer target, Color colour, float from, float to, float duration, Ease ease, float delay)
        {
            target.color = new Color(colour.r, colour.g, colour.b, 0f);
            return Tween.Alpha(target, from, to, duration, ease, startDelay: delay);
        }

        public void At<T>(T target, float delay, System.Action<T> action) where T : class
        {
            if (delay <= 0f) action(target);
            else Tween.Delay(target, delay, action, useUnscaledTime: true);
        }
    }

    // Where a cleared piece leaves the board (board-local) and when, as a delay from the current batch.
    public readonly struct ClearedPiece
    {
        public readonly int Id;
        public readonly SpriteRenderer Renderer;
        public readonly Vector3 Local;
        public readonly float Launch;

        public ClearedPiece(int id, SpriteRenderer renderer, Vector3 local, float launch)
        {
            Id = id;
            Renderer = renderer;
            Local = local;
            Launch = launch;
        }
    }
}
