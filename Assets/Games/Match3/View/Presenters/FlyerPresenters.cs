using Match3.Model;
using PrimeTween;
using Tileworks.Kit.Haptics;
using Tileworks.Tiles.Model;
using Tileworks.Tiles.View;
using UnityEngine;

namespace Match3.View
{
    // The flyer swells, rises and turns to its target, then flies an arc nose first, dropping sparks. A combo's extra
    // flyers appear on the launch cell.
    public sealed class FlyerLaunchedPresenter : IEventPresenter
    {
        private readonly Match3PresenterKit _kit;
        public FlyerLaunchedPresenter(Match3PresenterKit kit) => _kit = kit;
        public ushort EventType => _kit.Ids.FlyerLaunched;

        public float Present(in BoardEvent e, float delay)
        {
            var launched = FlyerLaunched.From(e);
            var board = _kit.Board;
            var feel = _kit.Feel;
            var from = board.CellPosition(launched.Source);
            var to = board.CellPosition(launched.Target);
            var piece = board.Get(launched.Id) ?? board.Add(launched.Id, launched.Source, Piece.Flyer, 0, 0);
            board.Dress(piece, Piece.Flyer, 0, 0);
            board.Lift(piece);
            board.Unmask(piece);
            piece.transform.localPosition = from;

            var takeOff = _kit.Duration(feel.FlyerTakeOff);
            var flight = _kit.Duration(feel.FlyerFlight);
            var rise = from + Vector3.up * feel.FlyerRise;
            var control = (rise + to) * 0.5f + Vector3.up * (feel.FlyerArc * Vector3.Distance(rise, to));
            Tween.Scale(piece.transform, feel.FlyerTakeOffScale, takeOff, Ease.OutBack, startDelay: delay);
            Tween.LocalPosition(piece.transform, from, rise, takeOff, Ease.OutBack, startDelay: delay);
            Tween.LocalEulerAngles(piece.transform, Vector3.zero, new Vector3(0f, 0f, Facing(rise, control, feel.FlyerNose)), takeOff, Ease.OutBack,
                startDelay: delay);
            var nose = feel.FlyerNose;
            Tween.Custom(piece.transform, 0f, 1f, flight, (t, p) =>
            {
                t.localPosition = Bezier(rise, control, to, p);
                var ahead = Bezier(rise, control, to, Mathf.Min(1f, p + 0.02f));
                if (p < 0.98f) t.localEulerAngles = new Vector3(0f, 0f, Facing(t.localPosition, ahead, nose));
            }, Ease.InOutSine, startDelay: delay + takeOff);

            var every = _kit.Duration(feel.FlyerSparkEvery);
            for (var at = every; at < flight; at += every) _kit.At(piece, delay + takeOff + at, Spark);
            _kit.Feedback(Match3Cue.Flyer, HapticKind.Light, delay);
            return _kit.Hold(delay, takeOff + flight);
        }

        private void Spark(SpriteRenderer flyer)
        {
            var board = _kit.Board;
            var spark = board.Fx(board.Skin.Spark, flyer.transform.localPosition, Vector2.one * _kit.Feel.FlyerSparkSize, Color.white);
            var fade = _kit.Duration(_kit.Feel.FlyerSparkFade);
            Tween.Alpha(spark, 0.9f, 0f, fade, Ease.InQuad);
            Tween.Scale(spark.transform, _kit.Feel.FlyerSparkSize * 0.3f, fade, Ease.InQuad).OnComplete(board, b => b.Release(spark));
        }

        private static Vector3 Bezier(Vector3 a, Vector3 control, Vector3 b, float t) => Vector3.Lerp(Vector3.Lerp(a, control, t), Vector3.Lerp(control, b, t), t);

        private static float Facing(Vector3 from, Vector3 to, float nose) => Mathf.Atan2(to.y - from.y, to.x - from.x) * Mathf.Rad2Deg + nose;
    }

    // The flyer dives into its target with a shock ring; the target's hits follow on landing.
    public sealed class FlyerLandedPresenter : IEventPresenter
    {
        private readonly Match3PresenterKit _kit;
        public FlyerLandedPresenter(Match3PresenterKit kit) => _kit = kit;
        public ushort EventType => _kit.Ids.FlyerLanded;

        public float Present(in BoardEvent e, float delay)
        {
            var landed = FlyerLanded.From(e);
            var board = _kit.Board;
            var feel = _kit.Feel;
            var hit = _kit.Duration(feel.FlyerHit);
            var id = landed.Id;
            var piece = board.Get(id);
            if (piece != null) Tween.Scale(piece.transform, 0f, hit, Ease.InBack, startDelay: delay).OnComplete(board, b => b.Remove(id));

            var at = board.CellPosition(landed.Target);
            var duration = _kit.Duration(feel.RingDuration);
            var ring = board.Fx(board.Skin.Ring, at, Vector2.one * feel.RingScale.x * feel.FlyerRingSize, Color.clear);
            Tween.Scale(ring.transform, feel.RingScale.y * feel.FlyerRingSize, duration, Ease.OutCubic, startDelay: delay + hit);
            var glow = feel.SpecialGlow;
            _kit.Fade(ring, glow, glow.a, 0f, duration, Ease.InQuad, delay + hit).OnComplete(board, b => b.Release(ring));
            _kit.Feedback(Match3Cue.Bomb, HapticKind.Light, delay + hit, 1.4f);
            return _kit.Hold(delay, hit);
        }
    }
}
