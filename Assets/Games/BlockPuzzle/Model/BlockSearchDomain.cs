using System;
using Tileworks.Tiles.Model;
using Tileworks.Tiles.Search;

namespace BlockPuzzle.Model
{
    // Moves are placements of any offered piece; offers are random, so playouts measure difficulty rather than prove it.
    public sealed class BlockSearchDomain : ISearchDomain<BlockState, PlacePiece>
    {
        private readonly BlockRules _rules;
        private readonly BlockLevel _level;
        private readonly RuleEngine<BlockState> _engine;

        public BlockSearchDomain(BlockRules rules, BlockLevel level, ulong seed = 0)
        {
            _rules = rules ?? throw new ArgumentNullException(nameof(rules));
            _level = level ?? throw new ArgumentNullException(nameof(level));
            _engine = rules.CreateEngine(level, seed);
            _engine.RecordActions = false;
        }

        public int MaxMoves => BlockState.OfferSize * _level.CellCount;
        public BlockState CreateScratch() => _rules.CreateScratch(_level);
        public BlockState CreateStart() => _rules.CreateEngine(_level, 1).State;
        public void CopyTo(BlockState from, BlockState to) => from.CopyTo(to);
        public ulong Hash(BlockState s) => s.ComputeHash();
        public int Pressure(BlockState s) => s.FilledCount;

        public SearchStatus Status(BlockState s) =>
            s.Outcome == Outcome.Won ? SearchStatus.Won : s.Outcome == Outcome.Lost ? SearchStatus.Lost : SearchStatus.Ongoing;

        public int GetMoves(BlockState s, Span<PlacePiece> into)
        {
            var count = 0;
            for (var slot = 0; slot < BlockState.OfferSize; slot++)
            {
                if (s.Offer[slot] == BlockState.NoShape) continue;
                var shape = _rules.Shapes[s.Offer[slot]];
                for (var anchor = 0; anchor < s.CellCount; anchor++)
                {
                    if (Placement.Fits(s, shape, anchor)) into[count++] = new PlacePiece(slot, anchor);
                }
            }

            return count;
        }

        public void Apply(BlockState s, in PlacePiece move)
        {
            _engine.Attach(s);
            var result = _engine.Apply(move);
            if (!result.Accepted) throw new InvalidOperationException($"Search applied an illegal move {move}: {result.Rejected}");
        }

        // Clearing lines (with gems) first, then keeping the board compact.
        public int Score(BlockState s, in PlacePiece move)
        {
            var shape = _rules.Shapes[s.Offer[move.Slot]];
            var (rows, columns) = Placement.LinesCompletedBy(s, shape, move.Anchor);
            var gems = 0;
            for (var cell = 0; cell < s.CellCount; cell++)
            {
                if (s.Gems[cell] && ((rows & 1 << cell / s.Width) != 0 || (columns & 1 << cell % s.Width) != 0)) gems++;
            }

            return (Bits(rows) + Bits(columns)) * 1000 + gems * 400 + Contact(s, shape, move.Anchor) * 10;
        }

        private static int Contact(BlockState s, Shape shape, int anchor)
        {
            var contact = 0;
            for (var i = 0; i < shape.Count; i++)
            {
                var cell = Placement.CellOf(s, shape, anchor, i);
                int x = cell % s.Width, y = cell / s.Width;
                contact += Solid(s, x - 1, y) + Solid(s, x + 1, y) + Solid(s, x, y - 1) + Solid(s, x, y + 1);
            }

            return contact;
        }

        private static int Solid(BlockState s, int x, int y) =>
            x < 0 || y < 0 || x >= s.Width || y >= s.Height || s.Cells[y * s.Width + x] != 0 ? 1 : 0;

        private static int Bits(int mask)
        {
            var count = 0;
            for (; mask != 0; mask &= mask - 1) count++;
            return count;
        }
    }
}
