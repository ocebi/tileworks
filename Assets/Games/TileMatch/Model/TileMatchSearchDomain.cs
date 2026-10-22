using System;
using Tileworks.Tiles.Model;
using Tileworks.Tiles.Search;

namespace TileMatch.Model
{
    // Moves are tile selections only; boosters are a player resource, not part of solvability.
    public sealed class TileMatchSearchDomain : ISearchDomain<TileMatchState, SelectTile>
    {
        private readonly TileMatchRules _rules;
        private readonly TileMatchLevel _level;
        private readonly RuleEngine<TileMatchState> _engine;

        public TileMatchSearchDomain(TileMatchRules rules, TileMatchLevel level)
        {
            _rules = rules ?? throw new ArgumentNullException(nameof(rules));
            _level = level ?? throw new ArgumentNullException(nameof(level));
            _engine = rules.CreateEngine(level, 0);
            _engine.RecordActions = false;
        }

        public int MaxMoves => _level.TileCount;
        public TileMatchState CreateScratch() => _rules.CreateScratch(_level);
        public TileMatchState CreateStart() => _rules.CreateState(_level);
        public void CopyTo(TileMatchState from, TileMatchState to) => from.CopyTo(to);
        public ulong Hash(TileMatchState s) => s.ComputeHash();
        public int Pressure(TileMatchState s) => s.TrayCount;

        public SearchStatus Status(TileMatchState s) =>
            s.Outcome == Outcome.Won ? SearchStatus.Won : s.Outcome == Outcome.Lost ? SearchStatus.Lost : SearchStatus.Ongoing;

        public int GetMoves(TileMatchState s, Span<SelectTile> into)
        {
            Span<int> group = stackalloc int[SelectTileHandler.MaxGroup];
            var count = 0;
            for (var tile = 0; tile < s.TileCount; tile++)
            {
                if (SelectTileHandler.ValidateTile(s, _rules.Modifiers, tile, group).IsNone) into[count++] = new SelectTile(tile);
            }

            return count;
        }

        public void Apply(TileMatchState s, in SelectTile move)
        {
            _engine.Attach(s);
            var result = _engine.Apply(move);
            if (!result.Accepted) throw new InvalidOperationException($"Solver applied an illegal move {move}: {result.Rejected}");
        }

        // Completing a triple first, then pairing, then uncovering the most tiles.
        public int Score(TileMatchState s, in SelectTile move)
        {
            var kind = s.Kinds[move.Tile];
            var inTray = s.CountInTray(kind);
            var score = inTray == 2 ? 1000 : inTray == 1 ? 400 : 0;
            foreach (var below in _level.Cover.Covers(s.SlotOf(move.Tile)))
            {
                if (s.Cover.Covering[below] == 1 && s.Occupant[below] >= 0) score += 20;
            }

            return score - s.TrayCount * (inTray == 0 ? 30 : 0);
        }
    }
}
