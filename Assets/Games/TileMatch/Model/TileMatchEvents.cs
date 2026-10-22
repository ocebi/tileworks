using Tileworks.Tiles.Model;

namespace TileMatch.Model
{
    public readonly struct TileMatchEventIds
    {
        public readonly ushort TileLeftBoard;
        public readonly ushort TileMovedToTray;
        public readonly ushort TraySlotChanged;
        public readonly ushort TripleCleared;
        public readonly ushort TileUncovered;
        public readonly ushort TileCovered;
        public readonly ushort TileRevealed;
        public readonly ushort ModifierChanged;
        public readonly ushort TileSpawned;
        public readonly ushort TileKindChanged;
        public readonly ushort TileReturnedToBoard;
        public readonly ushort TrayCapacityChanged;
        public readonly ushort LevelWon;
        public readonly ushort LevelLost;

        public TileMatchEventIds(EventTypeRegistry r)
        {
            TileLeftBoard = r.Register(nameof(TileLeftBoard), EntityFields.Entity);
            TileMovedToTray = r.Register(nameof(TileMovedToTray), EntityFields.Entity);
            TraySlotChanged = r.Register(nameof(TraySlotChanged), EntityFields.Entity);
            TripleCleared = r.Register(nameof(TripleCleared), EntityFields.Entity | EntityFields.A | EntityFields.B);
            TileUncovered = r.Register(nameof(TileUncovered), EntityFields.Entity);
            TileCovered = r.Register(nameof(TileCovered), EntityFields.Entity);
            TileRevealed = r.Register(nameof(TileRevealed), EntityFields.Entity);
            ModifierChanged = r.Register(nameof(ModifierChanged), EntityFields.Entity);
            TileSpawned = r.Register(nameof(TileSpawned), EntityFields.Entity);
            TileKindChanged = r.Register(nameof(TileKindChanged), EntityFields.Entity);
            TileReturnedToBoard = r.Register(nameof(TileReturnedToBoard), EntityFields.Entity);
            TrayCapacityChanged = r.Register(nameof(TrayCapacityChanged), EntityFields.None);
            LevelWon = r.Register(nameof(LevelWon), EntityFields.None, barrier: true);
            LevelLost = r.Register(nameof(LevelLost), EntityFields.None, barrier: true);
        }
    }

    // Typed views: nothing outside this file reads BoardEvent.A/B/C directly.
    public readonly struct TileMovedToTray
    {
        public readonly int Tile;
        public readonly int Slot;
        private TileMovedToTray(int tile, int slot) { Tile = tile; Slot = slot; }
        public static void Emit(EventWriter w, in TileMatchEventIds ids, int tile, int slot) => w.Emit(ids.TileMovedToTray, tile, slot);
        public static TileMovedToTray From(in BoardEvent e) => new TileMovedToTray(e.Entity, e.A);
    }

    public readonly struct TraySlotChanged
    {
        public readonly int Tile;
        public readonly int Slot;
        private TraySlotChanged(int tile, int slot) { Tile = tile; Slot = slot; }
        public static void Emit(EventWriter w, in TileMatchEventIds ids, int tile, int slot) => w.Emit(ids.TraySlotChanged, tile, slot);
        public static TraySlotChanged From(in BoardEvent e) => new TraySlotChanged(e.Entity, e.A);
    }

    public readonly struct TripleCleared
    {
        public readonly int First;
        public readonly int Second;
        public readonly int Third;
        public readonly ushort Kind;
        private TripleCleared(int a, int b, int c, ushort kind) { First = a; Second = b; Third = c; Kind = kind; }
        public static void Emit(EventWriter w, in TileMatchEventIds ids, int a, int b, int c, ushort kind) => w.Emit(ids.TripleCleared, a, b, c, kind);
        public static TripleCleared From(in BoardEvent e) => new TripleCleared(e.Entity, e.A, e.B, (ushort)e.C);
    }

    public readonly struct TileRevealed
    {
        public readonly int Tile;
        public readonly ushort Kind;
        private TileRevealed(int tile, ushort kind) { Tile = tile; Kind = kind; }
        public static void Emit(EventWriter w, in TileMatchEventIds ids, int tile, ushort kind) => w.Emit(ids.TileRevealed, tile, kind);
        public static TileRevealed From(in BoardEvent e) => new TileRevealed(e.Entity, (ushort)e.A);
    }

    public readonly struct ModifierChanged
    {
        public readonly int Tile;
        public readonly ushort ModifierType;
        public readonly int Value;
        private ModifierChanged(int tile, ushort type, int value) { Tile = tile; ModifierType = type; Value = value; }
        public static void Emit(EventWriter w, in TileMatchEventIds ids, int tile, ushort type, int value) => w.Emit(ids.ModifierChanged, tile, type, value);
        public static ModifierChanged From(in BoardEvent e) => new ModifierChanged(e.Entity, (ushort)e.A, e.B);
    }

    public readonly struct TileSpawned
    {
        public readonly int Tile;
        public readonly int Slot;
        private TileSpawned(int tile, int slot) { Tile = tile; Slot = slot; }
        public static void Emit(EventWriter w, in TileMatchEventIds ids, int tile, int slot) => w.Emit(ids.TileSpawned, tile, slot);
        public static TileSpawned From(in BoardEvent e) => new TileSpawned(e.Entity, e.A);
    }

    public readonly struct TileKindChanged
    {
        public readonly int Tile;
        public readonly ushort Kind;
        private TileKindChanged(int tile, ushort kind) { Tile = tile; Kind = kind; }
        public static void Emit(EventWriter w, in TileMatchEventIds ids, int tile, ushort kind) => w.Emit(ids.TileKindChanged, tile, kind);
        public static TileKindChanged From(in BoardEvent e) => new TileKindChanged(e.Entity, (ushort)e.A);
    }

    public readonly struct TileReturnedToBoard
    {
        public readonly int Tile;
        public readonly int Slot;
        private TileReturnedToBoard(int tile, int slot) { Tile = tile; Slot = slot; }
        public static void Emit(EventWriter w, in TileMatchEventIds ids, int tile, int slot) => w.Emit(ids.TileReturnedToBoard, tile, slot);
        public static TileReturnedToBoard From(in BoardEvent e) => new TileReturnedToBoard(e.Entity, e.A);
    }

    public readonly struct TrayCapacityChanged
    {
        public readonly int Capacity;
        private TrayCapacityChanged(int capacity) => Capacity = capacity;
        public static void Emit(EventWriter w, in TileMatchEventIds ids, int capacity) => w.Emit(ids.TrayCapacityChanged, -1, capacity);
        public static TrayCapacityChanged From(in BoardEvent e) => new TrayCapacityChanged(e.A);
    }

    public readonly struct LevelLost
    {
        public readonly LoseReason Reason;
        private LevelLost(LoseReason reason) => Reason = reason;
        public static void Emit(EventWriter w, in TileMatchEventIds ids, LoseReason reason) => w.Emit(ids.LevelLost, -1, (int)reason);
        public static LevelLost From(in BoardEvent e) => new LevelLost((LoseReason)e.A);
    }
}
