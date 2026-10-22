using System;
using Tileworks.Tiles.Model;

namespace Match3.Model
{
    // Board operations shared by the handlers and systems: hits, layers, clearing and goal bookkeeping.
    public sealed class Match3Board
    {
        public const int PointsPerPiece = 10;

        private static readonly int[] Dx = { 1, 0, -1, 0 };
        private static readonly int[] Dy = { 0, 1, 0, -1 };

        private readonly Match3EventIds _ids;

        public Match3Board(Match3EventIds ids) => _ids = ids;

        public Match3EventIds Ids => _ids;

        public static int Neighbour(Match3State s, int cell, int direction)
        {
            var n = s.CellAt(s.X(cell) + Dx[direction], s.Y(cell) + Dy[direction]);
            return n >= 0 && s.IsPlayable(n) ? n : -1;
        }

        public static int Distance(Match3State s, int a, int b) => Math.Max(Math.Abs(s.X(a) - s.X(b)), Math.Abs(s.Y(a) - s.Y(b)));

        public static bool AreNeighbours(Match3State s, int a, int b) => Math.Abs(s.X(a) - s.X(b)) + Math.Abs(s.Y(a) - s.Y(b)) == 1;

        // Boxes and chained gems hold their cell: nothing falls through them.
        public static bool IsStatic(Match3State s, int cell) => s.Pieces[cell] == Piece.Box || s.Chained[cell];

        public static bool CanMove(Match3State s, int cell) => s.Pieces[cell] != Piece.Empty && !IsStatic(s, cell);

        public static int NewId(Match3State s) => s.NextId++;

        // The piece comes to rest: it can match again, and a match it makes leaves its special here.
        public static void Land(Match3State s, int cell)
        {
            s.Flight.Land(cell);
            s.Landed[cell] = ++s.LandClock;
        }

        public static Effect EffectOf(Piece piece) => piece switch
        {
            Piece.RocketRow => Effect.Row,
            Piece.RocketColumn => Effect.Column,
            Piece.Bomb => Effect.Bomb,
            Piece.Disco => Effect.Disco,
            Piece.Flyer => Effect.Flyer,
            _ => Effect.None,
        };

        // Disco > bomb > flyer > rocket; the stronger of the two decides the combo.
        public static Effect Combine(Piece first, Piece second)
        {
            var (high, low) = Rank(first) >= Rank(second) ? (first, second) : (second, first);
            return (Rank(high), Rank(low)) switch
            {
                (4, 4) => Effect.ClearAll,
                (4, 3) => Effect.DiscoBombs,
                (4, 2) => Effect.DiscoFlyers,
                (4, 1) => Effect.DiscoRockets,
                (3, 3) => Effect.BigBomb,
                (3, 2) => Effect.FlyerBomb,
                (3, 1) => Effect.ThickCross,
                (2, 2) => Effect.TripleFlyer,
                (2, 1) => low == Piece.RocketRow ? Effect.FlyerRow : Effect.FlyerColumn,
                _ => Effect.Cross,
            };
        }

        private static int Rank(Piece p) => p switch
        {
            Piece.Disco => 4,
            Piece.Bomb => 3,
            Piece.Flyer => 2,
            _ => 1,
        };

        // The colour with the most gems, lowest first on a tie; 0 when no gem is left.
        public static byte MostCommonColour(Match3State s)
        {
            Span<int> counts = stackalloc int[Match3Level.MaxColours + 1];
            for (var cell = 0; cell < s.CellCount; cell++)
                if (s.Pieces[cell] == Piece.Gem) counts[s.Colours[cell]]++;
            byte best = 0;
            for (var c = 1; c < counts.Length; c++)
                if (counts[c] > counts[best]) best = (byte)c;
            return best;
        }

        // Swaps what two cells hold; floors and chains stay with their cells.
        public static void Exchange(Match3State s, int a, int b)
        {
            (s.Pieces[a], s.Pieces[b]) = (s.Pieces[b], s.Pieces[a]);
            (s.Colours[a], s.Colours[b]) = (s.Colours[b], s.Colours[a]);
            (s.Hp[a], s.Hp[b]) = (s.Hp[b], s.Hp[a]);
            (s.Ids[a], s.Ids[b]) = (s.Ids[b], s.Ids[a]);
        }

        // A piece hit in flight leaves the board too; the view's later landing report for it is refused.
        public void Remove(Match3State s, RuleContext ctx, int cell, int order, Effect cause = Effect.None, int into = -1)
        {
            var piece = s.Pieces[cell];
            var colour = s.Colours[cell];
            PieceCleared.Emit(ctx.Events, _ids, s.Ids[cell], cell, piece, colour, order, cause, into);
            s.Flight.Land(cell);
            s.Pieces[cell] = Piece.Empty;
            s.Colours[cell] = 0;
            s.Hp[cell] = 0;
            s.Score += PointsPerPiece;
            switch (piece)
            {
                case Piece.Gem:
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

        public void BreakChain(Match3State s, RuleContext ctx, int cell, int order)
        {
            s.Chained[cell] = false;
            ChainBroken.Emit(ctx.Events, _ids, s.Ids[cell], cell, order);
            Progress(s, ctx, GoalKind.Chain, 0);
        }

        public void DamageFloor(Match3State s, RuleContext ctx, int cell, int order)
        {
            if (s.Floors[cell] == 0) return;
            s.Floors[cell]--;
            FloorDamaged.Emit(ctx.Events, _ids, s.FloorIds[cell], cell, s.Floors[cell], order);
            if (s.Floors[cell] == 0) Progress(s, ctx, GoalKind.Floor, 0);
        }

        // A match next to a blocker: boxes lose a layer, balloons pop. Chains only break from a match that includes them.
        public void DamageNextTo(Match3State s, RuleContext ctx, int cell, int order)
        {
            if (s.Pieces[cell] == Piece.Balloon) Remove(s, ctx, cell, order);
            else if (s.Pieces[cell] == Piece.Box) DamageBox(s, ctx, cell, order);
        }

        // A special's effect reaching a cell takes its top layer: the chain, else the box, else the piece and one floor
        // layer. A special it reaches goes off in the next wave.
        public void Hit(Match3State s, RuleContext ctx, int cell, int order, Effect cause)
        {
            if (s.Chained[cell])
            {
                BreakChain(s, ctx, cell, order);
                return;
            }

            var piece = s.Pieces[cell];
            if (piece == Piece.Box)
            {
                DamageBox(s, ctx, cell, order, cause);
                return;
            }

            DamageFloor(s, ctx, cell, order);
            if (piece == Piece.Empty || piece == Piece.Duck) return;
            if (Pieces.IsSpecial(piece)) Queue(s, cell, EffectOf(piece), 0);
            else Remove(s, ctx, cell, order, cause);
        }

        // A disco queued with colour 0 picks the most common colour when it fires.
        public void Queue(Match3State s, int cell, Effect effect, byte colour)
        {
            s.Next[s.NextCount++] = new PendingSpecial(cell, s.Ids[cell], effect, colour);
            s.Flight.Land(cell);
            s.Pieces[cell] = Piece.Empty;
            s.Colours[cell] = 0;
        }

        private void DamageBox(Match3State s, RuleContext ctx, int cell, int order, Effect cause = Effect.None)
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

        private void Progress(Match3State s, RuleContext ctx, GoalKind kind, byte colour)
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
