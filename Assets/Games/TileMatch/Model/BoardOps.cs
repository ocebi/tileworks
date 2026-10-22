using System;
using Tileworks.Tiles.Model;

namespace TileMatch.Model
{
    // The only code that moves tiles between board, pending, tray and cleared; every move emits its event.
    public static class BoardOps
    {
        public static void RemoveFromBoard(TileMatchState s, int tile, int group, RuleContext ctx, in TileMatchEventIds ids)
        {
            var slot = s.SlotOf(tile);
            s.Locations[tile] = TileLocation.Pending;
            s.Occupant[slot] = -1;
            s.Vacated[s.VacatedCount++] = slot;
            s.Pending[s.PendingCount] = tile;
            s.PendingGroup[s.PendingCount++] = group;
            ctx.Events.Emit(ids.TileLeftBoard, tile);
        }

        public static void ReturnToBoard(TileMatchState s, int tile, RuleContext ctx, in TileMatchEventIds ids)
        {
            var slot = s.SlotOf(tile);
            if (s.Occupant[slot] >= 0) throw new InvalidOperationException($"Slot {slot} is occupied.");
            s.Locations[tile] = TileLocation.Board;
            s.Occupant[slot] = tile;
            Span<int> covered = stackalloc int[s.Level.Cover.Covers(slot).Length];
            var count = s.Cover.OnOccupied(s.Level.Cover, slot, covered);
            for (var i = 0; i < count; i++)
            {
                var below = s.Occupant[covered[i]];
                if (below >= 0) ctx.Events.Emit(ids.TileCovered, below);
            }

            TileReturnedToBoard.Emit(ctx.Events, ids, tile, slot);
        }

        // Inserts after the last tray tile of the same kind (or at the end) and shifts the rest right.
        public static void InsertIntoTray(TileMatchState s, int tile, int group, RuleContext ctx, in TileMatchEventIds ids)
        {
            var kind = s.Kinds[tile];
            var position = s.TrayCount;
            for (var i = s.TrayCount - 1; i >= 0; i--)
            {
                if (s.Kinds[s.Tray[i]] != kind) continue;
                position = i + 1;
                break;
            }

            for (var i = s.TrayCount; i > position; i--)
            {
                s.Tray[i] = s.Tray[i - 1];
                TraySlotChanged.Emit(ctx.Events, ids, s.Tray[i], i);
            }

            s.Tray[position] = tile;
            s.TrayCount++;
            s.Locations[tile] = TileLocation.Tray;
            PushInsert(s, tile, group);
            TileMovedToTray.Emit(ctx.Events, ids, tile, position);
        }

        public static void RemoveFromTray(TileMatchState s, int index, RuleContext ctx, in TileMatchEventIds ids)
        {
            for (var i = index; i < s.TrayCount - 1; i++)
            {
                s.Tray[i] = s.Tray[i + 1];
                TraySlotChanged.Emit(ctx.Events, ids, s.Tray[i], i);
            }

            s.TrayCount--;
        }

        public static int TrayIndexOf(TileMatchState s, int tile)
        {
            for (var i = 0; i < s.TrayCount; i++)
            {
                if (s.Tray[i] == tile) return i;
            }

            return -1;
        }

        public static void SetCapacity(TileMatchState s, int capacity, RuleContext ctx, in TileMatchEventIds ids)
        {
            s.TrayCapacity = capacity;
            TrayCapacityChanged.Emit(ctx.Events, ids, capacity);
        }

        // History only matters for tiles still in the tray, so a full stack compacts instead of growing.
        private static void PushInsert(TileMatchState s, int tile, int group)
        {
            if (s.InsertCount == s.InsertTile.Length) CompactInserts(s);
            s.InsertTile[s.InsertCount] = tile;
            s.InsertGroup[s.InsertCount++] = group;
        }

        private static void CompactInserts(TileMatchState s)
        {
            var write = 0;
            for (var read = 0; read < s.InsertCount; read++)
            {
                if (s.Locations[s.InsertTile[read]] != TileLocation.Tray) continue;
                s.InsertTile[write] = s.InsertTile[read];
                s.InsertGroup[write++] = s.InsertGroup[read];
            }

            s.InsertCount = write;
        }
    }
}
