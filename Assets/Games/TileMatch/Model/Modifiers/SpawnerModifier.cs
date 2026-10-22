using Tileworks.Tiles.Model;

namespace TileMatch.Model
{
    // data[spawner] = index of the next queued tile. A vacated spawner slot is refilled before uncovering runs,
    // so tiles beneath it never flicker uncovered.
    public sealed class SpawnerModifier : ITileModifier
    {
        public ushort TypeId => ModifierTypes.Spawner;

        public int[] CreateState(TileMatchLevel level) => new int[level.Spawners.Length];

        public bool OnRefill(TileMatchState s, int[] data, RuleContext ctx, in TileMatchEventIds ids)
        {
            var changed = false;
            var spawners = s.Level.Spawners;
            for (var i = 0; i < spawners.Length; i++)
            {
                var slot = spawners[i].Slot;
                var queue = spawners[i].Queue;
                if (s.Occupant[slot] >= 0 || data[i] >= queue.Length || !RemoveVacated(s, slot)) continue;

                var tile = queue[data[i]++];
                s.Locations[tile] = TileLocation.Board;
                s.Occupant[slot] = tile;
                TileSpawned.Emit(ctx.Events, ids, tile, slot);
                changed = true;
            }

            return changed;
        }

        private static bool RemoveVacated(TileMatchState s, int slot)
        {
            for (var i = 0; i < s.VacatedCount; i++)
            {
                if (s.Vacated[i] != slot) continue;
                s.Vacated[i] = s.Vacated[--s.VacatedCount];
                return true;
            }

            return false;
        }
    }
}
