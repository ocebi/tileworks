using System;
using Tileworks.Tiles.Model;

namespace TileMatch.Model
{
    public sealed class UncoverSystem : IRuleSystem<TileMatchState>
    {
        private readonly TileMatchEventIds _ids;

        public UncoverSystem(TileMatchEventIds ids) => _ids = ids;

        public bool Run(TileMatchState s, RuleContext ctx)
        {
            if (s.VacatedCount == 0) return false;

            var graph = s.Level.Cover;
            Span<int> uncovered = stackalloc int[graph.MaxCovers];
            for (var v = 0; v < s.VacatedCount; v++)
            {
                var slot = s.Vacated[v];
                if (s.Occupant[slot] >= 0) continue;
                var count = s.Cover.OnVacated(graph, slot, uncovered);
                for (var i = 0; i < count; i++)
                {
                    var tile = s.Occupant[uncovered[i]];
                    if (tile >= 0) ctx.Events.Emit(_ids.TileUncovered, tile);
                }
            }

            s.VacatedCount = 0;
            return true;
        }
    }
}
