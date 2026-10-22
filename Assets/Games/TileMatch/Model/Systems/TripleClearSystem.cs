using Tileworks.Tiles.Model;

namespace TileMatch.Model
{
    public sealed class TripleClearSystem : IRuleSystem<TileMatchState>
    {
        private readonly TileMatchEventIds _ids;

        public TripleClearSystem(TileMatchEventIds ids) => _ids = ids;

        public bool Run(TileMatchState s, RuleContext ctx)
        {
            for (var i = 0; i + 2 < s.TrayCount; i++)
            {
                var kind = s.Kinds[s.Tray[i]];
                if (s.Kinds[s.Tray[i + 1]] != kind || s.Kinds[s.Tray[i + 2]] != kind) continue;

                var a = s.Tray[i];
                var b = s.Tray[i + 1];
                var c = s.Tray[i + 2];
                s.Locations[a] = s.Locations[b] = s.Locations[c] = TileLocation.Cleared;
                TripleCleared.Emit(ctx.Events, _ids, a, b, c, kind);
                for (var k = 0; k < 3; k++) BoardOps.RemoveFromTray(s, i, ctx, _ids);
                s.TriplesCleared++;
                return true;
            }

            return false;
        }
    }
}
