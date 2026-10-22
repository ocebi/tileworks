namespace Match3.Model
{
    public static class Match3Hints
    {
        // The swap a hint nudges: one that makes the strongest special, else a swap that fires specials, else the biggest
        // match. False when the board has no swap, or while pieces are still falling.
        public static bool Best(Match3State s, out int from, out int to)
        {
            from = to = -1;
            if (s.Flight.Count > 0) return false;
            var best = 0;
            for (var cell = 0; cell < s.CellCount; cell++)
            {
                if (!Matches.CanSwap(s, cell)) continue;
                for (var d = 0; d < 2; d++)
                {
                    var n = Match3Board.Neighbour(s, cell, d);
                    if (!Matches.CanSwap(s, n)) continue;
                    var value = Value(s, cell, n);
                    if (value <= best) continue;
                    (best, from, to) = (value, cell, n);
                }
            }

            return best > 0;
        }

        // 0 for a swap the rules refuse.
        public static int Value(Match3State s, int from, int to)
        {
            var specials = (Pieces.IsSpecial(s.Pieces[from]) ? 1 : 0) + (Pieces.IsSpecial(s.Pieces[to]) ? 1 : 0);
            if (specials > 0) return 500 * specials;
            Match3Board.Exchange(s, from, to);
            var made = Matches.SpecialAt(s, to, out var size);
            var other = Matches.SpecialAt(s, from, out var otherSize);
            Match3Board.Exchange(s, from, to);
            if (other > made) made = other;
            return size + otherSize + (made == Piece.Empty ? 0 : 1000 + 100 * Rank(made));
        }

        private static int Rank(Piece p) => p switch
        {
            Piece.Disco => 4,
            Piece.Bomb => 3,
            Piece.Flyer => 2,
            _ => 1,
        };
    }
}
