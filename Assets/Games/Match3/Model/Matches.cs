using Tileworks.Tiles.Model;

namespace Match3.Model
{
    // Lines of 3 or more and 2x2 squares of one gem colour among landed pieces. Chained gems match too; specials never do.
    public static class Matches
    {
        public static bool MatchAt(Match3State s, int cell)
        {
            if (s.Pieces[cell] != Piece.Gem || s.Flight[cell]) return false;
            int x = s.X(cell), y = s.Y(cell);
            var colour = s.Colours[cell];
            if (Run(s, x, y, 1, 0, colour) >= 3 || Run(s, x, y, 0, 1, colour) >= 3) return true;
            for (var dy = -1; dy <= 0; dy++)
            for (var dx = -1; dx <= 0; dx++)
                if (Square(s, x + dx, y + dy, colour)) return true;
            return false;
        }

        public static bool AnyMatch(Match3State s)
        {
            for (var cell = 0; cell < s.CellCount; cell++)
                if (MatchAt(s, cell)) return true;
            return false;
        }

        public static bool CanSwap(Match3State s, int cell)
        {
            if (cell < 0 || !s.IsPlayable(cell) || s.Chained[cell] || s.Flight[cell]) return false;
            var piece = s.Pieces[cell];
            return piece != Piece.Empty && piece != Piece.Box;
        }

        // Whether swapping two gems (or a gem with a balloon or duck) lines up a match. The board is left as it was.
        public static bool MakesMatch(Match3State s, int a, int b)
        {
            Match3Board.Exchange(s, a, b);
            var match = MatchAt(s, a) || MatchAt(s, b);
            Match3Board.Exchange(s, a, b);
            return match;
        }

        // A landed special, or a swap that makes a match.
        public static bool HasMove(Match3State s)
        {
            for (var cell = 0; cell < s.CellCount; cell++)
                if (Pieces.IsSpecial(s.Pieces[cell]) && !s.Flight[cell]) return true;
            for (var cell = 0; cell < s.CellCount; cell++)
            {
                if (!CanSwap(s, cell)) continue;
                for (var d = 0; d < 2; d++)
                {
                    var n = Match3Board.Neighbour(s, cell, d);
                    if (CanSwap(s, n) && MakesMatch(s, cell, n)) return true;
                }
            }

            return false;
        }

        // The special a match through `cell` would leave: disco > bomb > flyer > rocket. Fills s.Group with the match.
        public static Piece SpecialAt(Match3State s, int cell, out int size)
        {
            size = 0;
            if (!MatchAt(s, cell)) return Piece.Empty;
            size = Collect(s, cell, 0);
            return Shape(s, size);
        }

        // Marks every matched cell with `mark` in s.Matched and returns how many there are.
        public static int MarkAll(Match3State s, int mark)
        {
            var count = 0;
            for (var cell = 0; cell < s.CellCount; cell++)
            {
                if (!MatchAt(s, cell)) continue;
                s.Matched[cell] = mark;
                count++;
            }

            return count;
        }

        // Fills s.Group with the matched cells of one colour connected to `cell`, and returns how many. Mark 0 checks each
        // cell instead of reading the marks from MarkAll.
        public static int Collect(Match3State s, int cell, int mark)
        {
            var colour = s.Colours[cell];
            var seen = s.NewMark();
            var top = 0;
            var count = 0;
            s.Stack[top++] = cell;
            s.Marks[cell] = seen;
            while (top > 0)
            {
                var current = s.Stack[--top];
                s.Group[count++] = current;
                for (var d = 0; d < 4; d++)
                {
                    var n = Match3Board.Neighbour(s, current, d);
                    if (n < 0 || s.Marks[n] == seen || s.Pieces[n] != Piece.Gem || s.Colours[n] != colour) continue;
                    if (mark != 0 ? s.Matched[n] != mark : !MatchAt(s, n)) continue;
                    s.Marks[n] = seen;
                    s.Stack[top++] = n;
                }
            }

            return count;
        }

        // The special for the match in s.Group. A horizontal 4 makes a column rocket.
        public static Piece Shape(Match3State s, int count)
        {
            int longestRow = 0, longestColumn = 0;
            bool crossing = false, square = false;
            for (var i = 0; i < count; i++)
            {
                var cell = s.Group[i];
                int x = s.X(cell), y = s.Y(cell);
                var colour = s.Colours[cell];
                var row = Run(s, x, y, 1, 0, colour);
                var column = Run(s, x, y, 0, 1, colour);
                if (row > longestRow) longestRow = row;
                if (column > longestColumn) longestColumn = column;
                crossing |= row >= 3 && column >= 3;
                square |= Square(s, x, y, colour);
            }

            if (longestRow >= 5 || longestColumn >= 5) return Piece.Disco;
            if (crossing) return Piece.Bomb;
            if (square) return Piece.Flyer;
            if (longestRow == 4) return Piece.RocketColumn;
            if (longestColumn == 4) return Piece.RocketRow;
            return Piece.Empty;
        }

        private static bool Same(Match3State s, int cell, byte colour) => cell >= 0 && s.Pieces[cell] == Piece.Gem && s.Colours[cell] == colour && !s.Flight[cell];

        // Length of the line of `colour` through (x, y) along (dx, dy).
        private static int Run(Match3State s, int x, int y, int dx, int dy, byte colour)
        {
            var length = 1;
            for (int i = x + dx, j = y + dy; Same(s, s.CellAt(i, j), colour); i += dx, j += dy) length++;
            for (int i = x - dx, j = y - dy; Same(s, s.CellAt(i, j), colour); i -= dx, j -= dy) length++;
            return length;
        }

        private static bool Square(Match3State s, int x, int y, byte colour) =>
            Same(s, s.CellAt(x, y), colour) && Same(s, s.CellAt(x + 1, y), colour) && Same(s, s.CellAt(x, y + 1), colour) && Same(s, s.CellAt(x + 1, y + 1), colour);
    }

    // Clears every match on the board at once; each connected match leaves its special where its newest piece landed.
    public sealed class MatchClearer
    {
        private readonly Match3Board _board;

        public MatchClearer(Match3Board board) => _board = board;

        public bool ClearAll(Match3State s, RuleContext ctx)
        {
            var mark = s.NewMark();
            if (Matches.MarkAll(s, mark) == 0) return false;
            for (var cell = 0; cell < s.CellCount; cell++)
            {
                if (s.Matched[cell] != mark) continue;
                var count = Matches.Collect(s, cell, mark);
                for (var i = 0; i < count; i++) s.Matched[s.Group[i]] = 0;
                Clear(s, ctx, count);
            }

            s.Matches++;
            return true;
        }

        private void Clear(Match3State s, RuleContext ctx, int count)
        {
            var special = Matches.Shape(s, count);
            var at = Newest(s, count);
            if (at < 0) special = Piece.Empty;
            if (at < 0) at = s.Group[0];
            var into = special == Piece.Empty ? -1 : at;

            var inMatch = s.MarkStamp;
            for (var i = 0; i < count; i++)
            {
                var cell = s.Group[i];
                var order = Match3Board.Distance(s, at, cell);
                if (s.Chained[cell])
                {
                    _board.BreakChain(s, ctx, cell, order);
                    continue;
                }

                _board.Remove(s, ctx, cell, order, Effect.None, into);
                _board.DamageFloor(s, ctx, cell, order);
            }

            // Each blocker next to the match is damaged once, however many matched cells touch it.
            var damaged = s.NewMark();
            for (var i = 0; i < count; i++)
            {
                var cell = s.Group[i];
                for (var d = 0; d < 4; d++)
                {
                    var n = Match3Board.Neighbour(s, cell, d);
                    if (n < 0 || s.Marks[n] == inMatch || s.Marks[n] == damaged) continue;
                    s.Marks[n] = damaged;
                    _board.DamageNextTo(s, ctx, n, Match3Board.Distance(s, at, n));
                }
            }

            if (special == Piece.Empty) return;
            s.Pieces[at] = special;
            s.Colours[at] = 0;
            s.Ids[at] = Match3Board.NewId(s);
            SpecialCreated.Emit(ctx.Events, _board.Ids, s.Ids[at], at, special);
        }

        // The unchained cell whose piece landed last; -1 when every cell is chained.
        private static int Newest(Match3State s, int count)
        {
            var best = -1;
            for (var i = 0; i < count; i++)
            {
                var cell = s.Group[i];
                if (s.Chained[cell]) continue;
                if (best < 0 || s.Landed[cell] > s.Landed[best] || (s.Landed[cell] == s.Landed[best] && cell < best)) best = cell;
            }

            return best;
        }
    }
}
