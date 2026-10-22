using System;
using Tileworks.Tiles.Model;

namespace TileMatch.Model
{
    public static class ModifierTypes
    {
        public const ushort Frozen = 1;
        public const ushort Hidden = 2;
        public const ushort Linked = 3;
        public const ushort Spawner = 4;
    }

    public readonly struct ModifierSpec
    {
        public readonly ushort Type;
        public readonly int Tile;
        public readonly int Value;

        public ModifierSpec(ushort type, int tile, int value = 0)
        {
            Type = type;
            Tile = tile;
            Value = value;
        }
    }

    public readonly struct SpawnerSpec
    {
        public readonly int Slot;
        private readonly int[] _queue;

        public SpawnerSpec(int slot, int[] queuedTiles)
        {
            Slot = slot;
            _queue = queuedTiles ?? Array.Empty<int>();
        }

        public ReadOnlySpan<int> Queue => _queue;
    }

    // Immutable; shared by every state and snapshot of the level. Tiles [0, BoardTileCount) start on the board,
    // the rest start queued in spawners.
    public sealed class TileMatchLevel
    {
        private readonly ushort[] _kinds;
        private readonly int[] _tileSlots;
        private readonly ModifierSpec[] _modifiers;
        private readonly SpawnerSpec[] _spawners;

        public TileMatchLevel(string id, LayeredLayout layout, ushort[] kinds, int[] tileSlots, int boardTileCount,
            int trayCapacity, int kindCount, ModifierSpec[] modifiers = null, SpawnerSpec[] spawners = null, int timeLimitSec = 0)
        {
            Id = id ?? string.Empty;
            Layout = layout ?? throw new ArgumentNullException(nameof(layout));
            _kinds = kinds ?? throw new ArgumentNullException(nameof(kinds));
            _tileSlots = tileSlots ?? throw new ArgumentNullException(nameof(tileSlots));
            if (kinds.Length != tileSlots.Length) throw new ArgumentException("kinds and tileSlots differ in length.");
            BoardTileCount = boardTileCount;
            TrayCapacity = trayCapacity;
            KindCount = kindCount;
            _modifiers = modifiers ?? Array.Empty<ModifierSpec>();
            _spawners = spawners ?? Array.Empty<SpawnerSpec>();
            TimeLimitSec = timeLimitSec;
        }

        public string Id { get; }
        public LayeredLayout Layout { get; }
        public CoverGraph Cover => Layout.Cover;
        public int SlotCount => Layout.CellCount;
        public int TileCount => _kinds.Length;
        public int BoardTileCount { get; }
        public int TrayCapacity { get; }
        public int KindCount { get; }
        public int TimeLimitSec { get; }
        public ReadOnlySpan<ushort> Kinds => _kinds;
        public ReadOnlySpan<int> TileSlots => _tileSlots;
        public ReadOnlySpan<ModifierSpec> Modifiers => _modifiers;
        public ReadOnlySpan<SpawnerSpec> Spawners => _spawners;

        public TileMatchLevel WithKinds(string id, ushort[] kinds, int kindCount) =>
            new TileMatchLevel(id, Layout, kinds, _tileSlots, BoardTileCount, TrayCapacity, kindCount, _modifiers, _spawners, TimeLimitSec);

        public int SlotOf(int tile) => _tileSlots[tile];
        public ushort KindOf(int tile) => _kinds[tile];
    }
}
