using Tileworks.Tiles.Model;

namespace TileMatch.Model
{
    // Inserts one pending tile per pass, so a triple is checked after every single insertion (§2.2).
    public sealed class PendingInsertSystem : IRuleSystem<TileMatchState>
    {
        private readonly TileMatchEventIds _ids;

        public PendingInsertSystem(TileMatchEventIds ids) => _ids = ids;

        public bool Run(TileMatchState s, RuleContext ctx)
        {
            if (s.PendingCount == 0 || s.TrayCount >= s.TrayCapacity) return false;

            var tile = s.Pending[0];
            var group = s.PendingGroup[0];
            s.PendingCount--;
            for (var i = 0; i < s.PendingCount; i++)
            {
                s.Pending[i] = s.Pending[i + 1];
                s.PendingGroup[i] = s.PendingGroup[i + 1];
            }

            BoardOps.InsertIntoTray(s, tile, group, ctx, _ids);
            return true;
        }
    }
}
