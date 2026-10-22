using Blast.Model;
using PrimeTween;
using Tileworks.Kit.Haptics;
using Tileworks.Tiles.Model;
using Tileworks.Tiles.View;
using UnityEngine;

namespace Blast.View
{
    // Pieces go one after another, outwards from what cleared them: a tapped group squashes and shrinks (or slides onto
    // the special it makes), a special's targets pop on its rhythm, and a duck slides out under the board.
    public sealed class PieceClearedPresenter : IEventPresenter
    {

        private readonly BlastPresenterKit _kit;
        public PieceClearedPresenter(BlastPresenterKit kit) => _kit = kit;
        public ushort EventType => _kit.Ids.PieceCleared;

        public float Present(in BoardEvent e, float delay)
        {
            var cleared = PieceCleared.From(e);
            var board = _kit.Board;
            var feel = _kit.Feel;
            var piece = board.Get(cleared.Id);
            _kit.LastCleared = default;
            if (piece == null) return 0f;
            var id = cleared.Id;
            var duration = _kit.Duration(feel.ClearDuration);
            if (cleared.Piece == Piece.Duck)
            {
                Tween.LocalPositionY(piece.transform, piece.transform.localPosition.y - 1f, duration * 2f, Ease.InQuad, startDelay: delay);
                Tween.Alpha(piece, 1f, 0f, duration * 2f, startDelay: delay).OnComplete(board, b => b.Remove(id));
                _kit.LastCleared = new ClearedPiece(id, piece, board.CellPosition(cleared.Cell) + Vector3.down * 0.25f, delay + duration);
                _kit.Feedback(BlastCue.Duck, HapticKind.Light, delay);
                return duration * 2f;
            }

            if (cleared.Into >= 0)
            {
                var slideStart = delay + _kit.Duration(feel.MergeStagger * cleared.Order);
                var slide = _kit.Duration(feel.MergeDuration);
                board.Lift(piece);
                Tween.LocalPosition(piece.transform, board.CellPosition(cleared.Into), slide, Ease.InQuad, startDelay: slideStart)
                    .OnComplete(board, b => b.Remove(id));
                if (cleared.Order == 0) _kit.Feedback(BlastCue.Blast, HapticKind.Light, delay);
                _kit.LastCleared = new ClearedPiece(id, piece, board.CellPosition(cleared.Into), slideStart + slide);
                return slideStart - delay + slide;
            }

            var start = delay + _kit.Duration(PopDelay(feel, cleared));
            var squash = _kit.Duration(feel.ClearSquashTime);
            Tween.Scale(piece.transform, feel.ClearSquash, squash, Ease.OutQuad, startDelay: start)
                .Chain(Tween.Scale(piece.transform, 0f, duration, Ease.InBack))
                .OnComplete(board, b => b.Remove(id));
            var at = board.CellPosition(cleared.Cell);
            _kit.LastCleared = new ClearedPiece(id, piece, at, start + squash);
            if (IsDisco(cleared.Cause)) Ray(at, cleared, start, squash + duration);
            _kit.Shatter(at, cleared.Piece == Piece.Box ? feel.BoxDebris : board.Skin.ColourOf(cleared.Colour), start + squash);
            Sound(cleared, start);
            return start - delay + squash + duration;
        }

        private static float PopDelay(BlastFeelConfig feel, in PieceCleared cleared) => cleared.Cause switch
        {
            Effect.None => feel.ClearStagger * cleared.Order,
            Effect.Row or Effect.Column or Effect.Cross or Effect.ThickCross => feel.RocketPop(cleared.Order),
            Effect.Bomb or Effect.BigBomb or Effect.ClearAll => feel.BombPop(cleared.Order),
            _ => feel.DiscoPop(cleared.Order),
        };

        private static bool IsDisco(Effect effect) => effect == Effect.Disco || effect == Effect.DiscoRockets || effect == Effect.DiscoBombs;

        // A disco target: its ray reaches it with a rising zap, then it sparks as it pops.
        private void Ray(Vector3 at, in PieceCleared cleared, float pop, float duration)
        {
            var board = _kit.Board;
            var tint = board.Skin.ColourOf(cleared.Colour);
            var rayStart = Mathf.Max(0f, pop - _kit.Duration(_kit.Feel.DiscoPop(0)));
            _kit.Beam(_kit.DiscoAt, at, tint, rayStart, pop - rayStart);
            _kit.Sfx.PlayStep(BlastCue.DiscoZap, Mathf.Min(cleared.Order, _kit.Feel.PopSteps), rayStart);
            var spark = board.Fx(board.Skin.Spark, at, Vector2.one * 0.3f, Color.clear);
            _kit.Fade(spark, tint, 1f, 0f, duration, Ease.InQuad, pop);
            Tween.Scale(spark.transform, 1.3f, duration, Ease.OutQuad, startDelay: pop).OnComplete(board, b => b.Release(spark));
        }

        // A tapped group pops once; a special's targets pop one by one, a step higher each, up the path.
        private void Sound(in PieceCleared cleared, float at)
        {
            switch (cleared.Piece)
            {
                case Piece.Cube when cleared.Cause == Effect.None:
                    if (cleared.Order == 0) _kit.Feedback(BlastCue.Blast, HapticKind.Light, at);
                    break;
                case Piece.Cube:
                    _kit.Sfx.PlayStep(BlastCue.Blast, Mathf.Min(cleared.Order, _kit.Feel.PopSteps), at);
                    break;
                case Piece.Box:
                    _kit.Feedback(BlastCue.BoxBreak, HapticKind.Light, at);
                    break;
                case Piece.Balloon:
                    _kit.Feedback(BlastCue.Balloon, HapticKind.Light, at);
                    break;
            }
        }
    }

    public sealed class BoxDamagedPresenter : IEventPresenter
    {
        private readonly BlastPresenterKit _kit;
        public BoxDamagedPresenter(BlastPresenterKit kit) => _kit = kit;
        public ushort EventType => _kit.Ids.BoxDamaged;

        public float Present(in BoardEvent e, float delay)
        {
            var damaged = BoxDamaged.From(e);
            var piece = _kit.Board.Get(damaged.Id);
            if (piece == null) return 0f;
            var start = delay + _kit.Duration(_kit.Feel.ClearStagger * damaged.Order);
            var duration = _kit.Duration(_kit.Feel.ClearDuration);
            var hp = damaged.HpLeft;
            _kit.At(piece, start, p =>
            {
                _kit.Board.Dress(p, Piece.Box, 0, hp);
                Tween.PunchScale(p.transform, new Vector3(0.2f, 0.2f, 0f), duration);
            });
            _kit.Feedback(BlastCue.Box, HapticKind.Light, start);
            return start - delay + duration;
        }
    }

    public sealed class SpecialCreatedPresenter : IEventPresenter
    {
        private readonly BlastPresenterKit _kit;
        public SpecialCreatedPresenter(BlastPresenterKit kit) => _kit = kit;
        public ushort EventType => _kit.Ids.SpecialCreated;

        public float Present(in BoardEvent e, float delay)
        {
            var created = SpecialCreated.From(e);
            var piece = _kit.Board.Add(created.Id, created.Cell, created.Piece, created.Colour, 0);
            piece.transform.localScale = Vector3.zero;
            var start = delay + _kit.Duration(_kit.Feel.MergeDuration);
            var duration = _kit.Duration(_kit.Feel.SpecialAppear);
            Tween.Scale(piece.transform, 1f, duration, Ease.OutBack, startDelay: start);
            _kit.Feedback(BlastCue.Special, HapticKind.Medium, start);
            return start - delay + duration;
        }
    }

    public sealed class CubeConvertedPresenter : IEventPresenter
    {
        private readonly BlastPresenterKit _kit;
        public CubeConvertedPresenter(BlastPresenterKit kit) => _kit = kit;
        public ushort EventType => _kit.Ids.CubeConverted;

        public float Present(in BoardEvent e, float delay)
        {
            var converted = CubeConverted.From(e);
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
            return _kit.Hold(delay, start - delay + duration);
        }
    }

    // Falls start column by column away from where the move started, then move frame by frame (BlastFalls); a leg for a
    // piece still in the air continues from where it is, without a stagger.
    public sealed class PieceFellPresenter : IEventPresenter
    {
        private readonly BlastPresenterKit _kit;
        public PieceFellPresenter(BlastPresenterKit kit) => _kit = kit;
        public ushort EventType => _kit.Ids.PieceFell;

        public float Present(in BoardEvent e, float delay)
        {
            var fell = PieceFell.From(e);
            var board = _kit.Board;
            var piece = board.Get(fell.Id);
            if (piece == null) return 0f;
            var start = board.Falls.IsMoving(fell.Id) ? 0f : Stagger(_kit, fell.Target);
            return start + board.Falls.Fall(fell.Id, piece, board.CellPosition(fell.Source), board.CellPosition(fell.Target), fell.Target, fell.Lands,
                delay + start);
        }

        // Seconds a fall in this cell's column waits: a step per column away from the move's origin (the centre for -1).
        public static float Stagger(BlastPresenterKit kit, int cell)
        {
            var board = kit.Board;
            var origin = board.OriginCell >= 0 ? board.OriginCell % board.Width : (board.Width - 1) * 0.5f;
            return kit.Duration(kit.Feel.FallColumnStagger * Mathf.Abs(cell % board.Width - origin));
        }
    }

    public sealed class PieceSpawnedPresenter : IEventPresenter
    {
        private readonly BlastPresenterKit _kit;
        public PieceSpawnedPresenter(BlastPresenterKit kit) => _kit = kit;
        public ushort EventType => _kit.Ids.PieceSpawned;

        public float Present(in BoardEvent e, float delay)
        {
            var spawned = PieceSpawned.From(e);
            var board = _kit.Board;
            var piece = board.Add(spawned.Id, spawned.Cell, Piece.Cube, spawned.Colour, 0);
            var from = board.Falls.Spawn(piece, board.SpawnPosition(spawned.Cell, spawned.Drop));
            var start = PieceFellPresenter.Stagger(_kit, spawned.Cell);
            _kit.Fade(piece, Color.white, 0f, 1f, _kit.Duration(_kit.Feel.FallTime(1f)), Ease.Default, delay + start);
            return start + board.Falls.Fall(spawned.Id, piece, from, board.CellPosition(spawned.Cell), spawned.Cell, spawned.Lands, delay + start);
        }
    }

    public sealed class CubeRecolouredPresenter : IEventPresenter
    {
        private readonly BlastPresenterKit _kit;
        public CubeRecolouredPresenter(BlastPresenterKit kit) => _kit = kit;
        public ushort EventType => _kit.Ids.CubeRecoloured;

        public float Present(in BoardEvent e, float delay)
        {
            var recoloured = CubeRecoloured.From(e);
            var piece = _kit.Board.Get(recoloured.Id);
            if (piece == null) return 0f;
            var duration = _kit.Duration(_kit.Feel.SpecialAppear);
            var colour = recoloured.Colour;
            // Explicit sizes: a cube still squashing from its landing ends at full size too.
            _kit.At(piece, delay, p =>
            {
                _kit.Board.Dress(p, Piece.Cube, colour, 0);
                Tween.Scale(p.transform, new Vector3(1.2f, 1.2f, 1f), Vector3.one, duration, Ease.OutElastic);
            });
            return duration;
        }
    }

    public sealed class MovesChangedPresenter : IEventPresenter
    {
        private readonly BlastPresenterKit _kit;
        public MovesChangedPresenter(BlastPresenterKit kit) => _kit = kit;
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
        private readonly BlastPresenterKit _kit;
        public GoalChangedPresenter(BlastPresenterKit kit) => _kit = kit;
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
        private readonly BlastPresenterKit _kit;
        public LevelWonPresenter(BlastPresenterKit kit) => _kit = kit;
        public ushort EventType => _kit.Ids.LevelWon;

        public float Present(in BoardEvent e, float delay)
        {
            _kit.Feedback(BlastCue.Win, HapticKind.Success, delay);
            var landed = _kit.LandsAt - _kit.Clock.Now - delay + _kit.Duration(_kit.Feel.WinAfterLanding);
            return Mathf.Max(_kit.Duration(_kit.Feel.ResultDelay), landed);
        }
    }

    public sealed class LevelLostPresenter : IEventPresenter
    {
        private readonly BlastPresenterKit _kit;
        public LevelLostPresenter(BlastPresenterKit kit) => _kit = kit;
        public ushort EventType => _kit.Ids.LevelLost;

        public float Present(in BoardEvent e, float delay)
        {
            _kit.Feedback(BlastCue.Lose, HapticKind.Failure, delay);
            return _kit.Duration(_kit.Feel.ResultDelay);
        }
    }
}
