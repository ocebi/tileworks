using System;
using Tileworks.Tiles.Model;

namespace Blast.Model
{
    // Board operations shared by the handlers and systems: groups, clearing, damage and goal bookkeeping.
    public sealed class BlastBoard
    {
        public const int PointsPerPiece = 10;

        private static readonly int[] Dx = { 1, 0, -1, 0 };
        private static readonly int[] Dy = { 0, 1, 0, -1 };

        private readonly BlastEventIds _ids;

        public BlastBoard(BlastEventIds ids) => _ids = ids;

        public BlastEventIds Ids => _ids;

        public static int Neighbour(BlastState s, int cell, int direction)
        {
            var n = s.CellAt(s.X(cell) + Dx[direction], s.Y(cell) + Dy[direction]);
            return n >= 0 && s.IsPlayable(n) ? n : -1;
        }

        // One pass of one action.
        public static long Wave(RuleContext ctx) => (long)ctx.ActionIndex << 16 | ctx.Events.Step;

        public static int Distance(BlastState s, int a, int b) => Math.Max(Math.Abs(s.X(a) - s.X(b)), Math.Abs(s.Y(a) - s.Y(b)));

        // Fills s.Group with the landed cells connected to `cell` that hold the same colour of cube, or any special.
        public static int FindGroup(BlastState s, int cell)
        {
            if (s.Flight[cell]) return 0;
            var piece = s.Pieces[cell];
            var specials = Pieces.IsSpecial(piece);
            if (piece != Piece.Cube && !specials) return 0;
            var colour = s.Colours[cell];
            var mark = s.NewMark();
            var top = 0;
            var count = 0;
            s.Stack[top++] = cell;
            s.Marks[cell] = mark;
            while (top > 0)
            {
                var current = s.Stack[--top];
                s.Group[count++] = current;
                for (var d = 0; d < 4; d++)
                {
                    var n = Neighbour(s, current, d);
                    if (n < 0 || s.Marks[n] == mark || s.Flight[n]) continue;
                    var matches = specials ? Pieces.IsSpecial(s.Pieces[n]) : s.Pieces[n] == Piece.Cube && s.Colours[n] == colour;
                    if (!matches) continue;
                    s.Marks[n] = mark;
                    s.Stack[top++] = n;
                }
            }

            return count;
        }

        // Asked only once nothing is in flight; pieces in flight never count.
        public static bool HasMove(BlastState s)
        {
            for (var cell = 0; cell < s.CellCount; cell++)
            {
                if (s.Flight[cell]) continue;
                var piece = s.Pieces[cell];
                if (Pieces.IsSpecial(piece)) return true;
                if (piece != Piece.Cube) continue;
                for (var d = 0; d < 2; d++)
                {
                    var n = Neighbour(s, cell, d);
                    if (n >= 0 && !s.Flight[n] && s.Pieces[n] == Piece.Cube && s.Colours[n] == s.Colours[cell]) return true;
                }
            }

            return false;
        }

        public static int NewId(BlastState s) => s.NextId++;

        public static Effect EffectOf(Piece piece) => piece switch
        {
            Piece.RocketRow => Effect.Row,
            Piece.RocketColumn => Effect.Column,
            Piece.Bomb => Effect.Bomb,
            Piece.Disco => Effect.Disco,
            _ => Effect.None,
        };

        // The two strongest specials decide the combo: disco > bomb > rocket.
        public static Effect Combine(Piece first, Piece second)
        {
            int Rank(Piece p) => p == Piece.Disco ? 3 : p == Piece.Bomb ? 2 : 1;
            var (high, low) = Rank(first) >= Rank(second) ? (Rank(first), Rank(second)) : (Rank(second), Rank(first));
            return (high, low) switch
            {
                (3, 3) => Effect.ClearAll,
                (3, 2) => Effect.DiscoBombs,
                (3, 1) => Effect.DiscoRockets,
                (2, 2) => Effect.BigBomb,
                (2, 1) => Effect.ThickCross,
                _ => Effect.Cross,
            };
        }

        public void Remove(BlastState s, RuleContext ctx, int cell, int order, Effect cause = Effect.None, int into = -1)
        {
            var piece = s.Pieces[cell];
            var colour = s.Colours[cell];
            PieceCleared.Emit(ctx.Events, _ids, s.Ids[cell], cell, piece, colour, order, cause, into);
            s.Clear(cell);
            s.Score += PointsPerPiece;
            switch (piece)
            {
                case Piece.Cube:
                    Progress(s, ctx, GoalKind.Colour, colour);
                    break;
                case Piece.Box:
                    Progress(s, ctx, GoalKind.Box, 0);
                    break;
                case Piece.Balloon:
                    Progress(s, ctx, GoalKind.Balloon, 0);
                    break;
                case Piece.Duck:
                    Progress(s, ctx, GoalKind.Duck, 0);
                    break;
            }
        }

        // A blast next to a blocker: boxes lose a layer, balloons pop. Ducks and cubes are untouched.
        public void DamageNextTo(BlastState s, RuleContext ctx, int cell, int order)
        {
            if (s.Pieces[cell] == Piece.Balloon) Remove(s, ctx, cell, order);
            else if (s.Pieces[cell] == Piece.Box) DamageBox(s, ctx, cell, order);
        }

        // A special's effect reaching a cell. A special it reaches goes off in the next wave.
        public void Hit(BlastState s, RuleContext ctx, int cell, int order, Effect cause)
        {
            var piece = s.Pieces[cell];
            switch (piece)
            {
                case Piece.Empty:
                case Piece.Duck:
                    return;
                case Piece.Box:
                    DamageBox(s, ctx, cell, order, cause);
                    return;
                default:
                    if (Pieces.IsSpecial(piece)) Queue(s, cell, EffectOf(piece));
                    else Remove(s, ctx, cell, order, cause);
                    return;
            }
        }

        public void Queue(BlastState s, int cell, Effect effect)
        {
            s.Next[s.NextCount++] = new PendingSpecial(cell, s.Ids[cell], effect, s.Colours[cell]);
            s.Clear(cell);
        }

        private void DamageBox(BlastState s, RuleContext ctx, int cell, int order, Effect cause = Effect.None)
        {
            if (s.Hp[cell] > 1)
            {
                s.Hp[cell]--;
                BoxDamaged.Emit(ctx.Events, _ids, s.Ids[cell], cell, s.Hp[cell], order);
            }
            else
            {
                Remove(s, ctx, cell, order, cause);
            }
        }

        private void Progress(BlastState s, RuleContext ctx, GoalKind kind, byte colour)
        {
            var goals = s.Level.Goals;
            for (var i = 0; i < goals.Length; i++)
            {
                if (goals[i].Kind != kind || goals[i].Colour != colour || s.GoalsLeft[i] == 0) continue;
                s.GoalsLeft[i]--;
                GoalChanged.Emit(ctx.Events, _ids, i, s.GoalsLeft[i]);
            }
        }
    }
}
