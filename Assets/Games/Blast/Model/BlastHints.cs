namespace Blast.Model
{
    public static class BlastHints
    {
        // Fills `cells` with the group a hint points at (specials first, then the biggest group) and returns its size;
        // 0 when the board has no move or pieces are still in flight.
        public static int Best(BlastState s, int[] cells)
        {
            if (s.Flight.Count > 0) return 0;
            int best = -1, bestSize = 0, bestRank = -1;
            for (var cell = 0; cell < s.CellCount; cell++)
            {
                var size = BlastBoard.FindGroup(s, cell);
                var special = Pieces.IsSpecial(s.Pieces[cell]);
                if (size < 2 && !special) continue;
                var rank = (special ? 1000 : 0) + size;
                if (rank <= bestRank) continue;
                (best, bestSize, bestRank) = (cell, size, rank);
            }

            if (best < 0) return 0;
            BlastBoard.FindGroup(s, best);
            System.Array.Copy(s.Group, cells, bestSize);
            return bestSize;
        }
    }
}
