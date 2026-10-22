using Tileworks.Kit.Core;
using Tileworks.Tiles.Model;

namespace Match3.Model
{
    // When the settled board has no move, the free gems fly to new cells: no ready-made match, at least one move.
    // Blockers, balloons, ducks and chained gems stay, and empty cells (sealed pens) stay empty.
    public sealed class ShuffleSystem : IRuleSystem<Match3State>
    {
        private const int Attempts = 30;

        private readonly Match3EventIds _ids;

        public ShuffleSystem(Match3EventIds ids) => _ids = ids;

        public bool Run(Match3State s, RuleContext ctx)
        {
            var wave = ctx.Events.Step;
            if (s.PendingCount > 0 || s.ClearedWave == wave || s.MovedWave == wave || s.Flight.Count > 0 || Matches.HasMove(s)) return false;
            var count = Free(s);
            if (!Shuffle(s, count, ref ctx.Rng.Get(Match3Rules.ShuffleStream))) return false;

            Emit(s, ctx, _ids, count, false);
            s.Shuffles++;
            s.MovedWave = wave;
            return true;
        }

        // The pieces in s.Group that Shuffle moved, after a barrier.
        public static void Emit(Match3State s, RuleContext ctx, in Match3EventIds ids, int count, bool booster)
        {
            BoardShuffled.Emit(ctx.Events, ids, booster);
            for (var i = 0; i < count; i++)
            {
                var cell = s.Group[i];
                var from = s.Group[Find(s, count, s.Ids[cell])];
                if (from != cell) PieceShuffled.Emit(ctx.Events, ids, s.Ids[cell], from, cell);
            }
        }

        // Shuffles the pieces in s.Group in place. False when no arrangement of them makes a move. With `derange`, the
        // first half of the attempts start over with Sattolo's shuffle (one cycle, so no piece stays put) and only pass
        // if every piece still moved.
        public static bool Shuffle(Match3State s, int count, ref Pcg32 rng, bool derange = false)
        {
            if (count < 3) return false;
            for (var i = 0; i < count; i++)
            {
                var cell = s.Group[i];
                (s.SpareColours[i], s.SpareIds[i], s.SparePieces[i]) = (s.Colours[cell], s.Ids[cell], s.Pieces[cell]);
            }

            for (var attempt = 0; attempt < Attempts; attempt++)
            {
                var cycle = derange && attempt < Attempts / 2;
                if (cycle) Restore(s, count);
                for (var i = count - 1; i > 0; i--) Exchange(s, s.Group[i], s.Group[rng.NextInt(cycle ? i : i + 1)]);
                for (var pass = 0; pass < 3 && Matches.AnyMatch(s); pass++) Repair(s, count, ref rng);
                if (!Matches.AnyMatch(s) && Matches.HasMove(s) && (!cycle || AllMoved(s, count))) return true;
            }

            if (Force(s, count) && Matches.HasMove(s)) return true;
            Restore(s, count);
            return false;
        }

        // Fills s.Group with what the shuffle booster moves: gems and specials that aren't chained.
        public static int Movable(Match3State s)
        {
            var count = 0;
            for (var cell = 0; cell < s.CellCount; cell++)
                if ((s.Pieces[cell] == Piece.Gem || Pieces.IsSpecial(s.Pieces[cell])) && !s.Chained[cell]) s.Group[count++] = cell;
            return count;
        }

        // Fills s.Group with the gems a shuffle may move and returns how many.
        public static int Free(Match3State s)
        {
            var count = 0;
            for (var cell = 0; cell < s.CellCount; cell++)
                if (s.Pieces[cell] == Piece.Gem && !s.Chained[cell]) s.Group[count++] = cell;
            return count;
        }

        // Swaps each gem that sits in a match with one that leaves both cells out of any match.
        private static void Repair(Match3State s, int count, ref Pcg32 rng)
        {
            for (var i = 0; i < count; i++)
            {
                var cell = s.Group[i];
                if (!Matches.MatchAt(s, cell)) continue;
                var start = rng.NextInt(count);
                for (var k = 0; k < count; k++)
                {
                    var other = s.Group[(start + k) % count];
                    if (s.Colours[other] == s.Colours[cell]) continue;
                    Exchange(s, cell, other);
                    if (!Matches.MatchAt(s, cell) && !Matches.MatchAt(s, other)) break;
                    Exchange(s, cell, other);
                }
            }
        }

        // Last resort: four free gems in a row become k k m k, so swapping the last two lines up three.
        private static bool Force(Match3State s, int count)
        {
            for (var i = 0; i < count; i++)
            {
                var a = s.Group[i];
                int x = s.X(a), y = s.Y(a);
                for (var axis = 0; axis < 2; axis++)
                {
                    var b = Free(s, x + (axis == 0 ? 1 : 0), y + axis);
                    var c = Free(s, x + (axis == 0 ? 2 : 0), y + axis * 2);
                    var d = Free(s, x + (axis == 0 ? 3 : 0), y + axis * 3);
                    if (b < 0 || c < 0 || d < 0) continue;
                    var k = s.Colours[a];
                    if (!Place(s, count, b, k, a, -1) || !Place(s, count, d, k, a, b)) continue;
                    if (s.Colours[c] == k && !PlaceOther(s, count, c, k, a, b, d)) continue;
                    return true;
                }
            }

            return false;
        }

        // Swaps a gem of `colour` into `cell`, taking it from anywhere but the kept cells.
        private static bool Place(Match3State s, int count, int cell, byte colour, int keep1, int keep2)
        {
            if (s.Colours[cell] == colour) return true;
            for (var i = 0; i < count; i++)
            {
                var other = s.Group[i];
                if (other == cell || other == keep1 || other == keep2 || s.Colours[other] != colour) continue;
                Exchange(s, cell, other);
                return true;
            }

            return false;
        }

        private static bool PlaceOther(Match3State s, int count, int cell, byte colour, int keep1, int keep2, int keep3)
        {
            for (var i = 0; i < count; i++)
            {
                var other = s.Group[i];
                if (other == cell || other == keep1 || other == keep2 || other == keep3 || s.Colours[other] == colour) continue;
                Exchange(s, cell, other);
                return true;
            }

            return false;
        }

        private static int Free(Match3State s, int x, int y)
        {
            var cell = s.CellAt(x, y);
            return cell >= 0 && s.IsPlayable(cell) && s.Pieces[cell] == Piece.Gem && !s.Chained[cell] ? cell : -1;
        }

        private static void Exchange(Match3State s, int a, int b)
        {
            (s.Pieces[a], s.Pieces[b]) = (s.Pieces[b], s.Pieces[a]);
            (s.Colours[a], s.Colours[b]) = (s.Colours[b], s.Colours[a]);
            (s.Ids[a], s.Ids[b]) = (s.Ids[b], s.Ids[a]);
        }

        private static void Restore(Match3State s, int count)
        {
            for (var i = 0; i < count; i++)
            {
                var cell = s.Group[i];
                (s.Colours[cell], s.Ids[cell], s.Pieces[cell]) = (s.SpareColours[i], s.SpareIds[i], s.SparePieces[i]);
            }
        }

        private static bool AllMoved(Match3State s, int count)
        {
            for (var i = 0; i < count; i++)
                if (s.Ids[s.Group[i]] == s.SpareIds[i]) return false;
            return true;
        }

        private static int Find(Match3State s, int count, int id)
        {
            for (var i = 0; i < count; i++)
                if (s.SpareIds[i] == id) return i;
            return -1;
        }
    }
}
