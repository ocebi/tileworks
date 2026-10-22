using Match3.Model;
using PrimeTween;
using Tileworks.Kit.Haptics;
using Tileworks.Tiles.Model;
using Tileworks.Tiles.View;
using UnityEngine;

namespace Match3.View
{
    // The other special in a combo is pulled onto the swapped one, drawing back slightly first.
    public sealed class SpecialMergedPresenter : IEventPresenter
    {
        private readonly Match3PresenterKit _kit;
        public SpecialMergedPresenter(Match3PresenterKit kit) => _kit = kit;
        public ushort EventType => _kit.Ids.SpecialMerged;

        public float Present(in BoardEvent e, float delay)
        {
            var merged = SpecialMerged.From(e);
            var board = _kit.Board;
            var piece = board.Get(merged.Id);
            if (piece == null) return 0f;
            var duration = _kit.Duration(_kit.Feel.ComboGather);
            var id = merged.Id;
            board.Lift(piece);
            Tween.Scale(piece.transform, _kit.Feel.ComboArrivalScale, duration, Ease.InQuad, startDelay: delay);
            Tween.LocalPosition(piece.transform, board.CellPosition(merged.Into), duration, Ease.InBack, startDelay: delay)
                .OnComplete(board, b => b.Remove(id));
            return _kit.Hold(delay, duration);
        }
    }

    // Once the others have arrived, the combined special swells and shakes while light and sparks gather into it, then
    // bursts with a flash, a shock ring and sparks as the combo goes off.
    public sealed class SpecialsCombinedPresenter : IEventPresenter
    {
        private readonly Match3PresenterKit _kit;
        public SpecialsCombinedPresenter(Match3PresenterKit kit) => _kit = kit;
        public ushort EventType => _kit.Ids.SpecialsCombined;

        public float Present(in BoardEvent e, float delay)
        {
            var combined = SpecialsCombined.From(e);
            var board = _kit.Board;
            var feel = _kit.Feel;
            var at = board.CellPosition(combined.Cell);
            var charge = delay + _kit.Duration(feel.ComboGather);
            var chargeTime = _kit.Duration(feel.ComboCharge);
            var burst = charge + chargeTime;
            var burstTime = _kit.Duration(feel.ComboBurst);
            var piece = board.Get(combined.Id);
            if (piece != null)
            {
                board.Lift(piece);
                Tween.Scale(piece.transform, feel.ComboSwell, chargeTime, Ease.InQuad, startDelay: charge);
                Tween.ShakeLocalRotation(piece.transform, new Vector3(0f, 0f, feel.ComboShakeDegrees), chargeTime, frequency: 14f, enableFalloff: false,
                    startDelay: charge);
            }

            var glow = board.Fx(board.Skin.Flash, at, Vector2.one * 0.8f, Color.clear);
            Tween.Scale(glow.transform, 2.4f, chargeTime, Ease.InQuad, startDelay: charge);
            _kit.Fade(glow, _kit.Feel.SpecialGlow, 0f, _kit.Feel.SpecialGlow.a, chargeTime, Ease.InQuad, charge).OnComplete(board, b => b.Release(glow));
            Ring(at, feel.RingScale.y, feel.RingScale.x, 0f, 1f, charge, chargeTime, Ease.InCubic);

            var flash = board.Fx(board.Skin.Flash, at, Vector2.one * 0.6f, Color.clear);
            Tween.Scale(flash.transform, 3.2f, burstTime, Ease.OutCubic, startDelay: burst);
            _kit.Fade(flash, Color.white, 1f, 0f, burstTime, Ease.InQuad, burst).OnComplete(board, b => b.Release(flash));
            Ring(at, feel.RingScale.x, feel.RingScale.y * 1.4f, 1f, 0f, burst, burstTime, Ease.OutCubic);

            var disco = combined.Effect == Effect.ClearAll || combined.Effect == Effect.DiscoRockets || combined.Effect == Effect.DiscoBombs || combined.Effect == Effect.DiscoFlyers;
            for (var i = 0; i < feel.ComboSparks; i++)
            {
                var angle = (i + combined.Cell * 0.37f) * Mathf.PI * 2f / feel.ComboSparks;
                var direction = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
                var tint = disco ? board.Skin.ColourOf((byte)(1 + i % 6)) : _kit.Feel.SpecialGlow;
                var spread = 0.8f + 0.4f * (i * 7 % feel.ComboSparks) / feel.ComboSparks;
                Spark(at + direction * feel.ComboSparkReach, at, tint, 0f, 1f, charge, chargeTime * spread, Ease.InQuad);
                Spark(at, at + direction * feel.ComboSparkReach * spread, tint, 1f, 0f, burst, burstTime * spread, Ease.OutCubic);
            }

            _kit.Feedback(Match3Cue.ComboGather, HapticKind.Light, charge);
            _kit.Feedback(Match3Cue.ComboBoom, HapticKind.Medium, burst);
            _kit.Punch(burst);
            _kit.Sweep(burst);
            _kit.Shake(feel.BombRadius, burst);
            return _kit.Hold(delay, burst - delay);
        }

        private void Ring(Vector3 at, float from, float to, float fromAlpha, float toAlpha, float delay, float duration, Ease ease)
        {
            var board = _kit.Board;
            var ring = board.Fx(board.Skin.Ring, at, Vector2.one * from, Color.clear);
            Tween.Scale(ring.transform, to, duration, ease, startDelay: delay);
            var glow = _kit.Feel.SpecialGlow;
            _kit.Fade(ring, glow, fromAlpha * glow.a, toAlpha * glow.a, duration, Ease.InQuad, delay).OnComplete(board, b => b.Release(ring));
        }

        private void Spark(Vector3 from, Vector3 to, Color tint, float fromAlpha, float toAlpha, float delay, float duration, Ease ease)
        {
            var board = _kit.Board;
            var size = _kit.Feel.ComboSparkSize;
            var spark = board.Fx(board.Skin.Spark, from, Vector2.one * size, Color.clear);
            Tween.LocalPosition(spark.transform, from, to, duration, ease, startDelay: delay);
            Tween.Scale(spark.transform, size * 0.3f, duration, Ease.InQuad, startDelay: delay);
            _kit.Fade(spark, tint, fromAlpha, toAlpha, duration, Ease.OutQuad, delay).OnComplete(board, b => b.Release(spark));
        }
    }

    // Rockets split into two halves that fly to the ends of their line; bombs wobble and send out a shock ring; disco balls
    // glow while their targets spark away. Everything waits for `delay`, so a special set off by another stays put until then.
    public sealed class SpecialFiredPresenter : IEventPresenter
    {
        private readonly Match3PresenterKit _kit;
        public SpecialFiredPresenter(Match3PresenterKit kit) => _kit = kit;
        public ushort EventType => _kit.Ids.SpecialFired;

        public float Present(in BoardEvent e, float delay)
        {
            var fired = SpecialFired.From(e);
            var board = _kit.Board;
            var feel = _kit.Feel;
            var at = board.CellPosition(fired.Cell);
            var piece = board.Get(fired.Id);
            var id = fired.Id;
            float busy;
            switch (fired.Effect)
            {
                case Effect.Row:
                case Effect.Column:
                case Effect.Cross:
                case Effect.ThickCross:
                    if (piece != null) _kit.At(piece, delay, _ => _kit.Board.Remove(id));
                    busy = _kit.Duration(feel.RocketFlight);
                    if (fired.Effect != Effect.Column) Halves(at, Vector3.right, board.Skin.SpriteOf(Piece.RocketRow, 0, 0), delay, busy);
                    if (fired.Effect != Effect.Row) Halves(at, Vector3.up, board.Skin.SpriteOf(Piece.RocketColumn, 0, 0), delay, busy);
                    _kit.Sparks(at, _kit.Feel.SpecialGlow, delay);
                    _kit.Feedback(Match3Cue.Rocket, HapticKind.Medium, delay);
                    break;
                case Effect.Bomb:
                case Effect.BigBomb:
                case Effect.ClearAll:
                    var wobble = _kit.Duration(feel.BombWobble);
                    if (piece != null)
                    {
                        board.Lift(piece);
                        Tween.ShakeLocalRotation(piece.transform, new Vector3(0f, 0f, feel.BombWobbleDegrees), wobble, startDelay: delay)
                            .OnComplete(board, b => b.Remove(id));
                    }

                    var size = fired.Effect == Effect.Bomb ? 1f : fired.Effect == Effect.BigBomb ? 1.4f : 2.5f;
                    busy = wobble + Ring(at, size, delay + wobble);
                    _kit.Sfx.Play(Match3Cue.BombFuse, delay);
                    _kit.Feedback(fired.Effect == Effect.Bomb ? Match3Cue.Bomb : Match3Cue.BigBoom, HapticKind.Medium, delay + wobble);
                    _kit.Explode(at, feel.BombRadius * size, feel.BombDebris, delay + wobble);
                    if (fired.Effect != Effect.Bomb)
                    {
                        _kit.Punch(delay + wobble);
                        _kit.Sweep(delay + wobble);
                    }
                    break;
                case Effect.Flyer:
                case Effect.FlyerRow:
                case Effect.FlyerColumn:
                case Effect.FlyerBomb:
                case Effect.TripleFlyer:
                    busy = 0f;
                    break;
                default:
                    busy = _kit.Duration(feel.DiscoRay);
                    if (piece != null)
                    {
                        board.Lift(piece);
                        Tween.Scale(piece.transform, 1.3f, busy, Ease.OutQuad, startDelay: delay);
                        Tween.Alpha(piece, 1f, 0f, busy * 2f, startDelay: delay).OnComplete(board, b => b.Remove(id));
                    }

                    Glowing(at, delay, busy * 2f);
                    _kit.DiscoAt = at;
                    _kit.Feedback(Match3Cue.DiscoCharge, HapticKind.Medium, delay);
                    _kit.Sfx.Play(Match3Cue.Disco, delay + busy * 2f);
                    break;
            }

            return _kit.Hold(delay, busy);
        }

        // Each half stays hidden until the rocket goes off.
        private void Halves(Vector3 at, Vector3 axis, Sprite sprite, float delay, float duration)
        {
            var board = _kit.Board;
            var reach = Mathf.Max(board.Width, board.Height);
            foreach (var direction in new[] { axis, -axis })
            {
                var half = board.Fx(sprite, at, Vector2.one, Color.clear);
                if (direction == -axis) half.transform.localRotation = Quaternion.Euler(0f, 0f, 180f);
                _kit.At(half, delay, h => h.color = Color.white);
                Tween.LocalPosition(half.transform, at, at + direction * reach, duration, Ease.InQuad, startDelay: delay);
                _kit.Trail(half.transform, _kit.Feel.SpecialGlow, delay, duration);
                Tween.Alpha(half, 1f, 0f, duration * 0.3f, startDelay: delay + duration * 0.7f).OnComplete(board, b => b.Release(half));
            }
        }

        private float Ring(Vector3 at, float size, float delay)
        {
            var board = _kit.Board;
            var feel = _kit.Feel;
            var duration = _kit.Duration(feel.RingDuration);
            var ring = board.Fx(board.Skin.Ring, at, Vector2.one * feel.RingScale.x * size, Color.clear);
            Tween.Scale(ring.transform, feel.RingScale.y * size, duration, Ease.OutCubic, startDelay: delay);
            _kit.Fade(ring, _kit.Feel.SpecialGlow, _kit.Feel.SpecialGlow.a, 0f, duration, Ease.InQuad, delay).OnComplete(board, b => b.Release(ring));
            return _kit.Hold(delay, duration);
        }

        private void Glowing(Vector3 at, float delay, float duration)
        {
            var board = _kit.Board;
            var glow = board.Fx(board.Skin.Flash, at, Vector2.one * 2.5f, Color.clear);
            _kit.Fade(glow, _kit.Feel.SpecialGlow, _kit.Feel.SpecialGlow.a, 0f, duration, Ease.InQuad, delay).OnComplete(board, b => b.Release(glow));
        }
    }
}
