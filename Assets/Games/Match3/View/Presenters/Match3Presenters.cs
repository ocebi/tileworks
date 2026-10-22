using Match3.Model;
using PrimeTween;
using Tileworks.Kit.Haptics;
using Tileworks.Tiles.Model;
using Tileworks.Tiles.View;
using UnityEngine;

namespace Match3.View
{
    // Pieces go one after another, outwards from what cleared them: a match squashes and shrinks (or slides onto the special
    // it makes), a special's targets pop on its rhythm, and a duck slides out under the board. A match waits for its swap.
    public sealed class PieceClearedPresenter : IEventPresenter
    {
        private readonly Match3PresenterKit _kit;
        public PieceClearedPresenter(Match3PresenterKit kit) => _kit = kit;
        public ushort EventType => _kit.Ids.PieceCleared;

        public float Present(in BoardEvent e, float delay)
        {
            var cleared = PieceCleared.From(e);
            var piece = _kit.Board.Get(cleared.Id);
            _kit.LastCleared = default;
            if (piece == null) return 0f;

            // The wait for the swap counts towards how long the clear keeps the board busy.
            var start = cleared.Cause == Effect.None ? _kit.AfterHold(delay) : delay;
            return start - delay + Clear(cleared, piece, start);
        }

        private float Clear(in PieceCleared cleared, SpriteRenderer piece, float delay)
        {
            var board = _kit.Board;
            var feel = _kit.Feel;
            var id = cleared.Id;
            var duration = _kit.Duration(cleared.Piece == Piece.Box ? feel.BoxClear : feel.ClearDuration);
            if (cleared.Piece == Piece.Duck)
            {
                Tween.LocalPositionY(piece.transform, piece.transform.localPosition.y - 1f, duration * 2f, Ease.InQuad, startDelay: delay);
                Tween.Alpha(piece, 1f, 0f, duration * 2f, startDelay: delay).OnComplete(board, b => b.Remove(id));
                _kit.LastCleared = new ClearedPiece(id, piece, board.CellPosition(cleared.Cell) + Vector3.down * 0.25f, delay + duration);
                _kit.Feedback(Match3Cue.Duck, HapticKind.Light, delay);
                return duration * 2f;
            }

            if (cleared.Into >= 0)
            {
                var slideStart = delay + _kit.Duration(feel.MergeStagger * cleared.Order);
                var slide = _kit.Duration(feel.MergeDuration);
                board.Lift(piece);

                // The piece already in the special's cell has nowhere to slide; it just goes when the others arrive.
                if (cleared.Into == cleared.Cell) _kit.At(board, slideStart + slide, b => b.Remove(id));
                else
                    Tween.LocalPosition(piece.transform, board.CellPosition(cleared.Into), slide, Ease.InQuad, startDelay: slideStart)
                        .OnComplete(board, b => b.Remove(id));
                if (cleared.Order == 0) MatchPop(delay);
                _kit.LastCleared = new ClearedPiece(id, piece, board.CellPosition(cleared.Into), slideStart + slide);
                return slideStart - delay + slide;
            }

            var start = delay + _kit.Duration(PopDelay(feel, cleared));
            var squash = _kit.Duration(feel.ClearSquashTime);
            Tween.Scale(piece.transform, cleared.Piece == Piece.Box ? feel.BoxSwell : feel.ClearSquash, squash, Ease.OutQuad, startDelay: start);
            Tween.Scale(piece.transform, 0f, duration, Ease.InBack, startDelay: start + squash).OnComplete(board, b => b.Remove(id));
            if (cleared.Piece == Piece.Box) Tween.Alpha(piece, 1f, 0f, duration, Ease.InQuad, startDelay: start + squash);
            var at = board.CellPosition(cleared.Cell);
            _kit.LastCleared = new ClearedPiece(id, piece, at, start + squash);
            if (IsDisco(cleared.Cause)) Ray(at, cleared, start, squash + duration);
            _kit.Shatter(at, cleared.Piece == Piece.Box ? feel.BoxDebris : board.Skin.ColourOf(cleared.Colour), start + squash);
            Sound(cleared, start);
            return start - delay + squash + duration;
        }

        private static float PopDelay(Match3FeelConfig feel, in PieceCleared cleared) => cleared.Cause switch
        {
            Effect.None or Effect.Flyer => feel.ClearStagger * cleared.Order,
            Effect.Row or Effect.Column or Effect.Cross or Effect.ThickCross => feel.RocketPop(cleared.Order),
            Effect.Bomb or Effect.BigBomb or Effect.ClearAll => feel.BombPop(cleared.Order),
            _ => feel.DiscoPop(cleared.Order),
        };

        private static bool IsDisco(Effect effect) => effect == Effect.Disco || effect == Effect.DiscoRockets || effect == Effect.DiscoBombs || effect == Effect.DiscoFlyers;

        // A disco target: its ray reaches it with a rising zap, then it sparks as it pops.
        private void Ray(Vector3 at, in PieceCleared cleared, float pop, float duration)
        {
            var board = _kit.Board;
            var tint = board.Skin.ColourOf(cleared.Colour);
            var rayStart = Mathf.Max(0f, pop - _kit.Duration(_kit.Feel.DiscoPop(0)));
            _kit.Beam(_kit.DiscoAt, at, tint, rayStart, pop - rayStart);
            _kit.Sfx.PlayStep(Match3Cue.DiscoZap, Mathf.Min(cleared.Order, _kit.Feel.PopSteps), rayStart);
            var spark = board.Fx(board.Skin.Spark, at, Vector2.one * 0.3f, Color.clear);
            _kit.Fade(spark, tint, 1f, 0f, duration, Ease.InQuad, pop);
            Tween.Scale(spark.transform, 1.3f, duration, Ease.OutQuad, startDelay: pop).OnComplete(board, b => b.Release(spark));
        }

        // Each match in a move pops a step higher than the last, so cascades climb.
        private void MatchPop(float at)
        {
            var step = Mathf.Min(_kit.Matches++, _kit.Feel.PopSteps);
            _kit.Sfx.PlayStep(Match3Cue.Match, step, at);
            _kit.Haptic(HapticKind.Light, at);
        }

        // A match pops once; a special's targets pop one by one, a step higher each, up the path.
        private void Sound(in PieceCleared cleared, float at)
        {
            switch (cleared.Piece)
            {
                case Piece.Gem when cleared.Cause == Effect.None:
                    if (cleared.Order == 0) MatchPop(at);
                    break;
                case Piece.Gem:
                    _kit.Sfx.PlayStep(Match3Cue.Match, Mathf.Min(cleared.Order, _kit.Feel.PopSteps), at);
                    break;
                case Piece.Box:
                    _kit.Feedback(Match3Cue.BoxBreak, HapticKind.Light, at);
                    break;
                case Piece.Balloon:
                    _kit.Feedback(Match3Cue.Balloon, HapticKind.Light, at);
                    break;
            }
        }
    }

    public sealed class BoxDamagedPresenter : IEventPresenter
    {
        private readonly Match3PresenterKit _kit;
        public BoxDamagedPresenter(Match3PresenterKit kit) => _kit = kit;
        public ushort EventType => _kit.Ids.BoxDamaged;

        public float Present(in BoardEvent e, float delay)
        {
            var damaged = BoxDamaged.From(e);
            var piece = _kit.Board.Get(damaged.Id);
            if (piece == null) return 0f;
            var start = _kit.AfterHold(delay) + _kit.Duration(_kit.Feel.ClearStagger * damaged.Order);
            var duration = _kit.Duration(_kit.Feel.BoxShake);
            var layers = damaged.LayersLeft;
            _kit.At(piece, start, p =>
            {
                _kit.Board.Dress(p, Piece.Box, 0, layers);
                Tween.ShakeLocalRotation(p.transform, new Vector3(0f, 0f, _kit.Feel.BoxShakeDegrees), duration, frequency: 20f);
            });
            _kit.Feedback(Match3Cue.Box, HapticKind.Light, start);
            return start - delay + duration;
        }
    }

    // A flash; with layers left the floor shows its last layer, at 0 it shrinks away.
    public sealed class FloorDamagedPresenter : IEventPresenter
    {
        private readonly Match3PresenterKit _kit;
        public FloorDamagedPresenter(Match3PresenterKit kit) => _kit = kit;
        public ushort EventType => _kit.Ids.FloorDamaged;

        public float Present(in BoardEvent e, float delay)
        {
            var damaged = FloorDamaged.From(e);
            var board = _kit.Board;
            var floor = board.GetFloor(damaged.Id);
            _kit.LastCleared = default;
            if (floor == null) return 0f;
            var start = _kit.AfterHold(delay) + _kit.Duration(_kit.Feel.ClearStagger * damaged.Order);
            var at = board.CellPosition(damaged.Cell);
            var flash = board.Fx(board.Skin.Flash, at, Vector2.one, Color.clear);
            _kit.Fade(flash, Color.white, 0.9f, 0f, _kit.Duration(_kit.Feel.FloorFlash), Ease.OutQuad, start).OnComplete(board, b => b.Release(flash));
            _kit.Feedback(Match3Cue.Floor, HapticKind.Light, start);
            if (damaged.LayersLeft > 0)
            {
                var sprite = board.Skin.Floor(damaged.LayersLeft);
                _kit.At(floor, start, f => f.sprite = sprite);
                return start - delay + _kit.Duration(_kit.Feel.FloorFlash);
            }

            var id = damaged.Id;
            var duration = _kit.Duration(_kit.Feel.FloorClear);
            Tween.Scale(floor.transform, _kit.Feel.FloorShrink, duration, Ease.InQuad, startDelay: start);
            Tween.Alpha(floor, 1f, 0f, duration, Ease.InQuad, startDelay: start).OnComplete(board, b => b.RemoveFloor(id));
            _kit.LastCleared = new ClearedPiece(-1, floor, at, start);
            return start - delay + duration;
        }
    }

    // The chain shakes, then swells, turns and fades with a spark while the freed gem hops.
    public sealed class ChainBrokenPresenter : IEventPresenter
    {
        private readonly Match3PresenterKit _kit;
        public ChainBrokenPresenter(Match3PresenterKit kit) => _kit = kit;
        public ushort EventType => _kit.Ids.ChainBroken;

        public float Present(in BoardEvent e, float delay)
        {
            var broken = ChainBroken.From(e);
            var board = _kit.Board;
            var feel = _kit.Feel;
            var gem = board.Get(broken.Id);
            _kit.LastCleared = default;
            if (gem == null || gem.transform.childCount == 0) return 0f;
            var chain = gem.transform.GetChild(0).GetComponent<SpriteRenderer>();
            var start = _kit.AfterHold(delay) + _kit.Duration(feel.ClearStagger * broken.Order);
            var shake = _kit.Duration(feel.ChainBreakShake);
            var fade = _kit.Duration(feel.ChainBreak);
            var at = board.CellPosition(broken.Cell);
            Tween.ShakeLocalRotation(chain.transform, new Vector3(0f, 0f, feel.ChainShakeDegrees), shake, frequency: 24f, startDelay: start);
            Tween.Scale(chain.transform, feel.ChainBreakSwell, fade, Ease.OutQuad, startDelay: start + shake);
            Tween.LocalEulerAngles(chain.transform, Vector3.zero, new Vector3(0f, 0f, feel.ChainBreakTwist), fade, Ease.OutQuad, startDelay: start + shake);
            Tween.Alpha(chain, 1f, 0f, fade, Ease.InQuad, startDelay: start + shake).OnComplete(board, b => b.Release(chain));
            var spark = board.Fx(board.Skin.Spark, at, Vector2.one * 0.5f, Color.clear);
            _kit.Fade(spark, Color.white, 1f, 0f, fade, Ease.InQuad, start + shake);
            Tween.Scale(spark.transform, 1.4f, fade, Ease.OutQuad, startDelay: start + shake).OnComplete(board, b => b.Release(spark));
            Tween.LocalPositionY(gem.transform, at.y + feel.ChainHop, _kit.Duration(feel.ChainHopUp), Ease.OutQuad, startDelay: start + shake);
            Tween.LocalPositionY(gem.transform, at.y, _kit.Duration(feel.ChainHopDown), Ease.OutBack, startDelay: start + shake + _kit.Duration(feel.ChainHopUp));
            _kit.Feedback(Match3Cue.ChainBreak, HapticKind.Light, start);
            _kit.Shatter(at, feel.ChainDebris, start + shake);
            _kit.LastCleared = new ClearedPiece(-1, chain, at, start + shake);
            return start - delay + shake + fade;
        }
    }

    public sealed class SpecialCreatedPresenter : IEventPresenter
    {
        private readonly Match3PresenterKit _kit;
        public SpecialCreatedPresenter(Match3PresenterKit kit) => _kit = kit;
        public ushort EventType => _kit.Ids.SpecialCreated;

        public float Present(in BoardEvent e, float delay)
        {
            var created = SpecialCreated.From(e);
            var piece = _kit.Board.Add(created.Id, created.Cell, created.Piece, 0, 0);
            piece.transform.localScale = Vector3.zero;
            var start = _kit.AfterHold(delay) + _kit.Duration(_kit.Feel.MergeDuration);
            var duration = _kit.Duration(_kit.Feel.SpecialAppear);
            Tween.Scale(piece.transform, 1f, duration, Ease.OutBack, startDelay: start);
            _kit.Feedback(Match3Cue.Special, HapticKind.Medium, start);
            return start - delay + duration;
        }
    }

    public sealed class GemConvertedPresenter : IEventPresenter
    {
        private readonly Match3PresenterKit _kit;
        public GemConvertedPresenter(Match3PresenterKit kit) => _kit = kit;
        public ushort EventType => _kit.Ids.GemConverted;

        public float Present(in BoardEvent e, float delay)
        {
            var converted = GemConverted.From(e);
            var piece = _kit.Board.Get(converted.Id);
            if (piece == null) return 0f;
            var start = delay + _kit.Duration(_kit.Feel.ClearStagger * converted.Order);
            var duration = _kit.Duration(_kit.Feel.SpecialAppear);
            var kind = converted.Piece;
            _kit.At(piece, start, p =>
            {
                _kit.Board.Dress(p, kind, 0, 0);
                Tween.PunchScale(p.transform, new Vector3(0.3f, 0.3f, 0f), duration);
            });
            return start - delay + duration;
        }
    }

    public sealed class CascadePresenter : IEventPresenter
    {
        private readonly Match3PresenterKit _kit;
        public CascadePresenter(Match3PresenterKit kit) => _kit = kit;
        public ushort EventType => _kit.Ids.Cascade;

        public float Present(in BoardEvent e, float delay)
        {
            var count = Cascade.From(e).Count;
            _kit.At(_kit.Hud, delay, hud => hud.ShowCascade(count));
            return 0f;
        }
    }

    public sealed class MovesChangedPresenter : IEventPresenter
    {
        private readonly Match3PresenterKit _kit;
        public MovesChangedPresenter(Match3PresenterKit kit) => _kit = kit;
        public ushort EventType => _kit.Ids.MovesChanged;

        public float Present(in BoardEvent e, float delay)
        {
            var left = MovesChanged.From(e).Left;
            _kit.At(_kit.Hud, delay, hud => hud.SetMoves(left));
            return 0f;
        }
    }

    // A piece that counts for a goal flies from where it left the board to the goal's chip; the chip counts on landing.
    public sealed class GoalChangedPresenter : IEventPresenter
    {
        private readonly Match3PresenterKit _kit;
        public GoalChangedPresenter(Match3PresenterKit kit) => _kit = kit;
        public ushort EventType => _kit.Ids.GoalChanged;

        public float Present(in BoardEvent e, float delay)
        {
            var changed = GoalChanged.From(e);
            var (goal, left) = (changed.Goal, changed.Left);
            var cleared = _kit.LastCleared;
            if (cleared.Renderer == null)
            {
                _kit.At(_kit.Hud, delay + _kit.Duration(_kit.Feel.ClearDuration), hud => hud.SetGoalLeft(goal, left));
                return 0f;
            }

            var board = _kit.Board;
            var flight = _kit.Flight;
            var world = board.WorldOf(cleared.Local);
            var sprite = cleared.Renderer.sprite;
            var width = sprite != null ? sprite.bounds.size.x * board.transform.lossyScale.x : 1f;
            _kit.At(_kit.Hud, cleared.Launch, hud =>
            {
                if (board.Get(cleared.Id) == cleared.Renderer) cleared.Renderer.enabled = false;
                var screen = board.Camera.WorldToScreenPoint(world);
                var edge = board.Camera.WorldToScreenPoint(world + Vector3.right * width);
                hud.Collect(goal, left, sprite, screen, edge.x - screen.x, flight);
            });
            _kit.LandsAt = Mathf.Max(_kit.LandsAt, _kit.Clock.Now + cleared.Launch + flight.Lift + flight.Duration);
            return 0f;
        }
    }

    public sealed class LevelWonPresenter : IEventPresenter
    {
        private readonly Match3PresenterKit _kit;
        public LevelWonPresenter(Match3PresenterKit kit) => _kit = kit;
        public ushort EventType => _kit.Ids.LevelWon;

        public float Present(in BoardEvent e, float delay)
        {
            _kit.Feedback(Match3Cue.Win, HapticKind.Success, delay);
            var landed = _kit.LandsAt - _kit.Clock.Now - delay + _kit.Duration(_kit.Feel.WinAfterLanding);
            return Mathf.Max(_kit.Duration(_kit.Feel.ResultDelay), landed);
        }
    }

    public sealed class LevelLostPresenter : IEventPresenter
    {
        private readonly Match3PresenterKit _kit;
        public LevelLostPresenter(Match3PresenterKit kit) => _kit = kit;
        public ushort EventType => _kit.Ids.LevelLost;

        public float Present(in BoardEvent e, float delay)
        {
            _kit.Feedback(Match3Cue.Lose, HapticKind.Failure, delay);
            return _kit.Duration(_kit.Feel.ResultDelay);
        }
    }
}
