using System;
using Tileworks.Kit.Core;
using Tileworks.Tiles.Model;

namespace TileMatch.Model
{
    public enum TileLocation : byte
    {
        Board,
        Tray,
        Cleared,
        Queued,
        Pending,
    }

    public enum LoseReason : byte
    {
        None,
        TrayFull,
        NoMoves,
        TimeUp,
    }

    // Plain arrays so copies and hashes are cheap; the solver and snapshot ring copy states freely.
    public sealed class TileMatchState : IRuleState<TileMatchState>
    {
        public const int MaxExtraSlots = 8;

        public readonly TileMatchLevel Level;
        public readonly ushort[] Kinds;
        public readonly TileLocation[] Locations;
        public readonly int[] Occupant;
        public CoverCounts Cover;

        public readonly int[] Tray;
        public int TrayCount;
        public int TrayCapacity;

        public readonly int[] InsertTile;
        public readonly int[] InsertGroup;
        public int InsertCount;
        public int NextGroup;

        public readonly int[] Pending;
        public readonly int[] PendingGroup;
        public int PendingCount;

        public readonly int[] Vacated;
        public int VacatedCount;

        public readonly int[][] ModifierData;

        public int TriplesCleared;
        public int ActionsTaken;
        public int BoosterExtraSlots;
        public int ContinuesUsed;
        public LoseReason LoseReason;

        public TileMatchState(TileMatchLevel level, int[][] modifierData)
        {
            Level = level ?? throw new ArgumentNullException(nameof(level));
            var tiles = level.TileCount;
            Kinds = new ushort[tiles];
            Locations = new TileLocation[tiles];
            Occupant = new int[level.SlotCount];
            Cover = new CoverCounts { Covering = new int[level.SlotCount] };
            Tray = new int[level.TrayCapacity + MaxExtraSlots];
            InsertTile = new int[tiles + Tray.Length];
            InsertGroup = new int[InsertTile.Length];
            Pending = new int[tiles];
            PendingGroup = new int[tiles];
            Vacated = new int[level.SlotCount];
            ModifierData = modifierData ?? Array.Empty<int[]>();
        }

        public Outcome Outcome { get; set; }
        public int TileCount => Kinds.Length;
        public int FreeTraySlots => TrayCapacity - TrayCount - PendingCount;

        public int SlotOf(int tile) => Level.SlotOf(tile);
        public bool IsOnBoard(int tile) => Locations[tile] == TileLocation.Board;
        public bool IsFree(int tile) => Locations[tile] == TileLocation.Board && Cover.Covering[Level.SlotOf(tile)] == 0;

        public int CountInTray(ushort kind)
        {
            var count = 0;
            for (var i = 0; i < TrayCount; i++)
            {
                if (Kinds[Tray[i]] == kind) count++;
            }

            return count;
        }

        public void CopyTo(TileMatchState t)
        {
            if (t.Level != Level) throw new ArgumentException("Snapshots must share the level.");
            Array.Copy(Kinds, t.Kinds, Kinds.Length);
            Array.Copy(Locations, t.Locations, Locations.Length);
            Array.Copy(Occupant, t.Occupant, Occupant.Length);
            Cover.CopyTo(ref t.Cover);
            Array.Copy(Tray, t.Tray, Tray.Length);
            Array.Copy(InsertTile, t.InsertTile, InsertCount);
            Array.Copy(InsertGroup, t.InsertGroup, InsertCount);
            Array.Copy(Pending, t.Pending, PendingCount);
            Array.Copy(PendingGroup, t.PendingGroup, PendingCount);
            Array.Copy(Vacated, t.Vacated, VacatedCount);
            for (var i = 0; i < ModifierData.Length; i++) Array.Copy(ModifierData[i], t.ModifierData[i], ModifierData[i].Length);
            t.TrayCount = TrayCount;
            t.TrayCapacity = TrayCapacity;
            t.InsertCount = InsertCount;
            t.NextGroup = NextGroup;
            t.PendingCount = PendingCount;
            t.VacatedCount = VacatedCount;
            t.TriplesCleared = TriplesCleared;
            t.ActionsTaken = ActionsTaken;
            t.BoosterExtraSlots = BoosterExtraSlots;
            t.ContinuesUsed = ContinuesUsed;
            t.LoseReason = LoseReason;
            t.Outcome = Outcome;
        }

        // Covers what the player can observe; undo history is excluded so the solver can merge equivalent states.
        public ulong ComputeHash()
        {
            var hash = StableHash.Combine(0x7E55E2A, (ulong)TrayCapacity);
            for (var i = 0; i < Kinds.Length; i++)
                hash = StableHash.Combine(hash, ((ulong)Kinds[i] << 8) | (byte)Locations[i]);
            for (var i = 0; i < Occupant.Length; i++) hash = StableHash.Combine(hash, (ulong)(uint)Occupant[i]);
            for (var i = 0; i < TrayCount; i++) hash = StableHash.Combine(hash, (ulong)Tray[i]);
            foreach (var data in ModifierData) hash = StableHash.Combine(hash, data);
            return StableHash.Combine(hash, ((ulong)Outcome << 8) | (byte)LoseReason);
        }
    }
}
